// PATHNC AUTOMATION - ShopDocDatabase.cs
//
// Salva a folha de processo (Shop Documentation) no PathNCAutomationDB.
//   ShopDocModel        : dados do cabecalho + lista de operacoes
//   ShopDocCollector    : le o setup CAM da Work Part via NXOpen/UF
//   ShopDocRepository   : grava em SQL Server numa unica transacao
//
// Connection string: variavel de ambiente PATHNC_DB_CONNECTION, ou arquivo
// PathNCAutomationDB.connection ao lado da DLL, ou LocalDB (fallback).

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Drawing.Imaging;
using NXOpen;
using NXOpen.CAM;
using NXOpen.UF;

namespace PathNCAutomation.ShopDoc
{
    // ====================================================================
    //  MODELO
    // ====================================================================
    public class ShopDocModel
    {
        public string PartName;
        public string PartPath;
        public string Language;
        public string Programmer;
        public string MachineName;
        public string HtmlFileName;
        public string HtmlContent;

        // Assinatura geometrica (usada na busca de pecas similares)
        public double? SizeX, SizeY, SizeZ;
        public double? VolumeMm3;
        public int? FaceCount;
        public int? BodyCount;

        // PNG da peca (isometrica + fit), capturado no save
        public byte[] Thumbnail;

        public List<ShopDocOperationModel> Operations = new List<ShopDocOperationModel>();

        public double TotalTimeMin
        {
            get { return Operations.Sum(o => o.TimeMin); }
        }
    }

    public class ShopDocOperationModel
    {
        public int Seq;
        public string ProgramGroup;
        public string OperationName;
        public string OperationType;
        public string ToolName;
        public int? ToolNumber;
        public double? ToolDiameter;
        public double? Rpm;
        public double? FeedCut;
        public string Geometry;
        public string Method;
        public double TimeMin;
    }

    // ====================================================================
    //  COLETOR (NX -> modelo)
    // ====================================================================
    public static class ShopDocCollector
    {
        public static ShopDocModel FromWorkPart(string language)
        {
            Session session = Session.GetSession();
            Part workPart = session.Parts.Work;
            if (workPart == null || workPart.CAMSetup == null)
                throw new InvalidOperationException("Abra uma peca com setup de CAM antes de salvar.");

            UFSession uf = UFSession.GetUFSession();
            CAMSetup setup = workPart.CAMSetup;

            ShopDocModel doc = new ShopDocModel
            {
                PartName = System.IO.Path.GetFileNameWithoutExtension(workPart.Leaf),
                PartPath = workPart.FullPath,
                Language = language,
                Programmer = Environment.UserName,
                MachineName = Environment.MachineName
            };

            int seq = 1;
            foreach (NXOpen.CAM.Operation op in setup.CAMOperationCollection)
            {
                ShopDocOperationModel m = new ShopDocOperationModel
                {
                    Seq = seq++,
                    OperationName = op.Name,
                    OperationType = op.GetType().Name,          // CavityMilling, HoleDrilling...
                    ProgramGroup = NomeSeguro(() => op.GetParent(CAMSetup.View.ProgramOrder)),
                    Geometry = NomeSeguro(() => op.GetParent(CAMSetup.View.Geometry)),
                    Method = NomeSeguro(() => op.GetParent(CAMSetup.View.MachineMethod)),
                    TimeMin = LerTempo(op)
                };

                NCGroup toolGroup = null;
                try { toolGroup = op.GetParent(CAMSetup.View.MachineTool); } catch { }
                if (toolGroup != null)
                {
                    m.ToolName = toolGroup.Name;
                    m.ToolNumber = LerInt(uf, toolGroup.Tag, UFConstants.UF_PARAM_TL_NUMBER);
                    m.ToolDiameter = LerDouble(uf, toolGroup.Tag, UFConstants.UF_PARAM_TL_DIAMETER);
                }

                m.Rpm = LerDouble(uf, op.Tag, UFConstants.UF_PARAM_SPINDLE_RPM);
                m.FeedCut = LerDouble(uf, op.Tag, UFConstants.UF_PARAM_FEED_CUT);

                doc.Operations.Add(m);
            }

            ColetarAssinatura(uf, workPart, doc);
            doc.Thumbnail = CapturarThumbnail(workPart);
            AnexarHtmlMaisRecente(doc);
            return doc;
        }

        // ------------------------------------------------------------
        // Captura a area grafica em PNG: forca vista isometrica com fit,
        // exporta, e restaura a vista que o usuario tinha. Reduz para
        // THUMB_WIDTH px de largura antes de devolver. Nunca lanca:
        // se falhar, devolve null e o save segue sem imagem.
        // ------------------------------------------------------------
        public const int THUMB_WIDTH = 480;

        public static byte[] CapturarThumbnail(Part workPart)
        {
            string tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "pathnc_thumb_" + Guid.NewGuid().ToString("N") + ".png");

            NXOpen.View workView = workPart.Views.WorkView;
            Matrix3x3 rot = new Matrix3x3();
            Point3d origin = new Point3d();
            double scale = 1.0;
            bool vistaSalva = false;

            try
            {
                try
                {
                    rot = workView.Matrix;
                    origin = workView.Origin;
                    scale = workView.Scale;
                    vistaSalva = true;
                }
                catch { }

                workView.Orient(NXOpen.View.Canned.Isometric, NXOpen.View.ScaleAdjustment.Fit);

                NXOpen.Gateway.ImageExportBuilder ieb = workPart.Views.CreateImageExportBuilder();
                try
                {
                    ieb.FileName = tmp;
                    ieb.FileFormat = NXOpen.Gateway.ImageExportBuilder.FileFormats.Png;
                    ieb.RegionMode = false;
                    ieb.EnhanceEdges = true;
                    ieb.BackgroundOption = NXOpen.Gateway.ImageExportBuilder.BackgroundOptions.Original;
                    ieb.Commit();
                }
                finally
                {
                    ieb.Destroy();
                }

                if (!File.Exists(tmp)) return null;

                using (Image original = Image.FromFile(tmp))
                {
                    int w = THUMB_WIDTH;
                    int h = (int)Math.Round(original.Height * (double)w / original.Width);
                    using (Bitmap reduzida = new Bitmap(w, h))
                    using (Graphics g = Graphics.FromImage(reduzida))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.DrawImage(original, 0, 0, w, h);
                        using (MemoryStream ms = new MemoryStream())
                        {
                            reduzida.Save(ms, ImageFormat.Png);
                            return ms.ToArray();
                        }
                    }
                }
            }
            catch
            {
                return null;
            }
            finally
            {
                try
                {
                    if (vistaSalva)
                        workView.SetRotationTranslationScale(rot, origin, scale);
                }
                catch { }
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            }
        }

        // Bounding box, volume e contagem de faces dos corpos solidos da peca.
        // Em setup de assembly (peca como componente), varre tambem os componentes.
        public static void ColetarAssinatura(UFSession uf, Part workPart, ShopDocModel doc)
        {
            double[] min = { double.MaxValue, double.MaxValue, double.MaxValue };
            double[] max = { double.MinValue, double.MinValue, double.MinValue };
            double volume = 0.0;
            int faces = 0, bodies = 0;

            foreach (Body body in TodosOsCorpos(workPart))
            {
                if (!body.IsSolidBody) continue;
                bodies++;
                try
                {
                    double[] box = new double[6];
                    uf.Modl.AskBoundingBox(body.Tag, box);
                    for (int i = 0; i < 3; i++)
                    {
                        if (box[i] < min[i]) min[i] = box[i];
                        if (box[i + 3] > max[i]) max[i] = box[i + 3];
                    }
                    faces += body.GetFaces().Length;

                    double[] acc = { 0.999 };
                    double[] props = new double[47];
                    double[] stats = new double[13];
                    uf.Modl.AskMassProps3d(new Tag[] { body.Tag }, 1, 1, 4, 1.0, 1, acc, props, stats);
                    volume += props[1];   // [0]=area, [1]=volume
                }
                catch { }
            }

            if (bodies == 0) return;
            doc.SizeX = max[0] - min[0];
            doc.SizeY = max[1] - min[1];
            doc.SizeZ = max[2] - min[2];
            doc.VolumeMm3 = volume;
            doc.FaceCount = faces;
            doc.BodyCount = bodies;
        }

        private static IEnumerable<Body> TodosOsCorpos(Part workPart)
        {
            foreach (Body b in workPart.Bodies) yield return b;

            NXOpen.Assemblies.Component root = workPart.ComponentAssembly.RootComponent;
            if (root == null) yield break;
            foreach (NXOpen.Assemblies.Component c in root.GetChildren())
            {
                Part p = c.Prototype as Part;
                if (p == null) continue;
                foreach (Body b in p.Bodies) yield return b;
            }
        }

        // Pega o HTML gerado mais recentemente em Documents\ShopDocumentation
        private static void AnexarHtmlMaisRecente(ShopDocModel doc)
        {
            try
            {
                string pasta = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ShopDocumentation");
                if (!Directory.Exists(pasta)) return;

                FileInfo ultimo = new DirectoryInfo(pasta)
                    .GetFiles("*.html")
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .FirstOrDefault();

                if (ultimo != null)
                {
                    doc.HtmlFileName = ultimo.Name;
                    doc.HtmlContent = File.ReadAllText(ultimo.FullName);
                }
            }
            catch { /* HTML e opcional */ }
        }

        private static string NomeSeguro(Func<NCGroup> getter)
        {
            try { NCGroup g = getter(); return g != null ? g.Name : null; }
            catch { return null; }
        }

        private static double LerTempo(NXOpen.CAM.Operation op)
        {
            try { return op.GetToolpathTime(); } catch { return 0.0; }
        }

        private static double? LerDouble(UFSession uf, Tag obj, int param)
        {
            try { double v; uf.Param.AskDoubleValue(obj, param, out v); return v; }
            catch { return null; }
        }

        private static int? LerInt(UFSession uf, Tag obj, int param)
        {
            try { int v; uf.Param.AskIntValue(obj, param, out v); return v; }
            catch { return null; }
        }
    }

    // ====================================================================
    //  REPOSITORIO (modelo -> SQL Server)
    // ====================================================================
    public class ShopDocRepository
    {
        private readonly string _connectionString;

        public ShopDocRepository() : this(ResolverConnectionString()) { }

        public ShopDocRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        // Ordem de resolucao (sem depender de System.Configuration):
        //   1) variavel de ambiente PATHNC_DB_CONNECTION
        //   2) arquivo PathNCAutomationDB.connection (texto puro, 1 linha) ao lado da DLL
        //   3) LocalDB padrao
        public static string ResolverConnectionString()
        {
            string env = Environment.GetEnvironmentVariable("PATHNC_DB_CONNECTION");
            if (!string.IsNullOrWhiteSpace(env))
                return env.Trim();

            try
            {
                string dir = System.IO.Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location);
                string arquivo = System.IO.Path.Combine(dir, "PathNCAutomationDB.connection");
                if (File.Exists(arquivo))
                {
                    string txt = File.ReadAllText(arquivo).Trim();
                    if (txt.Length > 0) return txt;
                }
            }
            catch { }

            return @"Server=(localdb)\MSSQLLocalDB;Database=PathNCAutomationDB;Integrated Security=true;";
        }

        // Retorna (ShopDocId, Revision) da linha gravada.
        public Tuple<int, int> Save(ShopDocModel doc)
        {
            using (SqlConnection cn = new SqlConnection(_connectionString))
            {
                cn.Open();
                using (SqlTransaction tx = cn.BeginTransaction())
                {
                    try
                    {
                        int revision = ProximaRevisao(cn, tx, doc.PartName);
                        int shopDocId = InserirCabecalho(cn, tx, doc, revision);
                        InserirOperacoes(cn, tx, shopDocId, doc.Operations);
                        tx.Commit();
                        return Tuple.Create(shopDocId, revision);
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }
        }

        private static int ProximaRevisao(SqlConnection cn, SqlTransaction tx, string partName)
        {
            using (SqlCommand cmd = new SqlCommand(
                "SELECT ISNULL(MAX(Revision), 0) + 1 FROM dbo.ShopDoc WHERE PartName = @p", cn, tx))
            {
                cmd.Parameters.AddWithValue("@p", partName);
                return (int)cmd.ExecuteScalar();
            }
        }

        private static int InserirCabecalho(SqlConnection cn, SqlTransaction tx, ShopDocModel doc, int revision)
        {
            const string sql = @"
INSERT INTO dbo.ShopDoc
    (PartName, PartPath, Revision, Language, Programmer, MachineName,
     OperationCount, TotalTimeMin, HtmlFileName, HtmlContent,
     SizeX, SizeY, SizeZ, VolumeMm3, FaceCount, BodyCount, Thumbnail)
OUTPUT INSERTED.ShopDocId
VALUES
    (@PartName, @PartPath, @Revision, @Language, @Programmer, @MachineName,
     @OperationCount, @TotalTimeMin, @HtmlFileName, @HtmlContent,
     @SizeX, @SizeY, @SizeZ, @VolumeMm3, @FaceCount, @BodyCount, @Thumbnail);";

            using (SqlCommand cmd = new SqlCommand(sql, cn, tx))
            {
                cmd.Parameters.AddWithValue("@PartName", doc.PartName);
                cmd.Parameters.AddWithValue("@PartPath", (object)doc.PartPath ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Revision", revision);
                cmd.Parameters.AddWithValue("@Language", (object)doc.Language ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Programmer", (object)doc.Programmer ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@MachineName", (object)doc.MachineName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@OperationCount", doc.Operations.Count);
                cmd.Parameters.AddWithValue("@TotalTimeMin", doc.TotalTimeMin);
                cmd.Parameters.AddWithValue("@HtmlFileName", (object)doc.HtmlFileName ?? DBNull.Value);
                cmd.Parameters.Add("@HtmlContent", SqlDbType.NVarChar, -1).Value =
                    (object)doc.HtmlContent ?? DBNull.Value;
                cmd.Parameters.AddWithValue("@SizeX", (object)doc.SizeX ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@SizeY", (object)doc.SizeY ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@SizeZ", (object)doc.SizeZ ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@VolumeMm3", (object)doc.VolumeMm3 ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@FaceCount", (object)doc.FaceCount ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@BodyCount", (object)doc.BodyCount ?? DBNull.Value);
                cmd.Parameters.Add("@Thumbnail", SqlDbType.VarBinary, -1).Value =
                    (object)doc.Thumbnail ?? DBNull.Value;
                return (int)cmd.ExecuteScalar();
            }
        }

        private static void InserirOperacoes(SqlConnection cn, SqlTransaction tx, int shopDocId,
                                             List<ShopDocOperationModel> ops)
        {
            const string sql = @"
INSERT INTO dbo.ShopDocOperation
    (ShopDocId, Seq, ProgramGroup, OperationName, OperationType, ToolName, ToolNumber,
     ToolDiameter, Rpm, FeedCut, Geometry, Method, TimeMin)
VALUES
    (@ShopDocId, @Seq, @ProgramGroup, @OperationName, @OperationType, @ToolName, @ToolNumber,
     @ToolDiameter, @Rpm, @FeedCut, @Geometry, @Method, @TimeMin);";

            using (SqlCommand cmd = new SqlCommand(sql, cn, tx))
            {
                cmd.Parameters.Add("@ShopDocId", SqlDbType.Int);
                cmd.Parameters.Add("@Seq", SqlDbType.Int);
                cmd.Parameters.Add("@ProgramGroup", SqlDbType.NVarChar, 100);
                cmd.Parameters.Add("@OperationName", SqlDbType.NVarChar, 100);
                cmd.Parameters.Add("@OperationType", SqlDbType.NVarChar, 50);
                cmd.Parameters.Add("@ToolName", SqlDbType.NVarChar, 100);
                cmd.Parameters.Add("@ToolNumber", SqlDbType.Int);
                cmd.Parameters.Add("@ToolDiameter", SqlDbType.Float);
                cmd.Parameters.Add("@Rpm", SqlDbType.Float);
                cmd.Parameters.Add("@FeedCut", SqlDbType.Float);
                cmd.Parameters.Add("@Geometry", SqlDbType.NVarChar, 100);
                cmd.Parameters.Add("@Method", SqlDbType.NVarChar, 100);
                cmd.Parameters.Add("@TimeMin", SqlDbType.Float);

                foreach (ShopDocOperationModel o in ops)
                {
                    cmd.Parameters["@ShopDocId"].Value = shopDocId;
                    cmd.Parameters["@Seq"].Value = o.Seq;
                    cmd.Parameters["@ProgramGroup"].Value = (object)o.ProgramGroup ?? DBNull.Value;
                    cmd.Parameters["@OperationName"].Value = o.OperationName;
                    cmd.Parameters["@OperationType"].Value = (object)o.OperationType ?? DBNull.Value;
                    cmd.Parameters["@ToolName"].Value = (object)o.ToolName ?? DBNull.Value;
                    cmd.Parameters["@ToolNumber"].Value = (object)o.ToolNumber ?? DBNull.Value;
                    cmd.Parameters["@ToolDiameter"].Value = (object)o.ToolDiameter ?? DBNull.Value;
                    cmd.Parameters["@Rpm"].Value = (object)o.Rpm ?? DBNull.Value;
                    cmd.Parameters["@FeedCut"].Value = (object)o.FeedCut ?? DBNull.Value;
                    cmd.Parameters["@Geometry"].Value = (object)o.Geometry ?? DBNull.Value;
                    cmd.Parameters["@Method"].Value = (object)o.Method ?? DBNull.Value;
                    cmd.Parameters["@TimeMin"].Value = o.TimeMin;
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}