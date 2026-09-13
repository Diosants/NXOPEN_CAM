// HoleDiameterColorizer.cs
// Utilitário reutilizável para PATHNC AUTOMATION
//
// Varre as faces cilíndricas de um Component (fluxo de assembly) OU de um
// Part direto (fluxo mais comum no PATHNC, onde tudo opera em cima de
// theSession.Parts.Work sem passar por Component de assembly), agrupa por
// diâmetro (com tolerância) e opcionalmente aplica uma cor distinta por
// grupo. Separado em duas camadas:
//   - ScanHolesByDiameter: só reconhece e agrupa (útil pra alimentar AutoDrill/FBM
//     ou pra checar cobertura de ferramental contra PathNCAutomationDB, sem tocar no display)
//   - ScanAndColorHolesByDiameter: reconhece + aplica cor (QC visual)
//
// Uso dentro do PATHNC (DLL in-process): basta adicionar esta classe ao projeto
// e chamar HoleDiameterColorizer.ScanAndColorHolesByDiameter(...) direto de qualquer
// form/handler que já tenha a Session.
//
// Uso como Journal standalone: ver NXJournal_Example.cs neste mesmo pacote.

using System;
using System.Collections.Generic;
using System.Linq;
using NXOpen;
using NXOpen.Assemblies;
using NXOpen.UF;

namespace PathNCAutomation.Utilities
{
    /// <summary>
    /// Reconhecimento e coloração de furos/faces cilíndricas por diâmetro.
    /// </summary>
    public static class HoleDiameterColorizer
    {
        /// <summary>
        /// Um grupo de faces cilíndricas que compartilham (dentro da tolerância) o mesmo diâmetro.
        /// </summary>
        public class DiameterGroup
        {
            public double Diameter { get; set; }
            public List<DisplayableObject> Faces { get; set; } = new List<DisplayableObject>();
            public int? AppliedColor { get; set; }
            public string AppliedColorName { get; set; }
        }

        // ── Paleta padrão: cyan, emerald, purple, yellow, red - as mesmas
        // cores (Tailwind) já usadas no resto do painel (ver ChatAssistantForm,
        // ColorAccentCyan/ColorPrimary/etc.).
        //
        // IMPORTANTE: NÃO usamos números de índice de cor fixos aqui. O
        // índice de cor do NX é ESPECÍFICO DE CADA ARQUIVO (cada .prt tem sua
        // própria Color Definition File/CDF) - o mesmo índice pode ser uma
        // cor num arquivo e outra completamente diferente em outro. Por
        // isso guardamos as cores como RGB e resolvemos pro índice real do
        // arquivo aberto em tempo de execução via UF_DISP_ask_closest_color
        // (ver ResolveDefaultColorPalette) - garante que "cyan" sai cyan
        // não importa qual .prt está aberto.
        private class NamedColor
        {
            public string Name;
            public byte R, G, B;
        }
        private static readonly NamedColor[] DefaultColorRgbPalette =
        {
            new NamedColor { Name = "cyan",    R = 34,  G = 211, B = 238 },
            new NamedColor { Name = "emerald", R = 16,  G = 185, B = 129 },
            new NamedColor { Name = "purple",  R = 139, G = 92,  B = 246 },
            new NamedColor { Name = "yellow",  R = 250, G = 204, B = 21  },
            new NamedColor { Name = "red",     R = 239, G = 68,  B = 68  },
        };

        // Cinza neutro usado por ResetHoleColorsToGray - "descolorir" na
        // prática, já que não existe remoção de cor por face na API de
        // display do NX (ver comentário lá embaixo).
        private static readonly NamedColor GrayColor = new NamedColor { Name = "gray", R = 156, G = 163, B = 175 };

        // Resolve cada cor nomeada (RGB) pro índice de cor mais próximo na
        // Color Definition File do arquivo atualmente aberto, usando a
        // função UF_DISP_ask_closest_color (documentada na API UF_DISP do
        // NXOpen). Isso substitui a antiga lista de índices fixos - método
        // correto porque índice de cor não é portável entre arquivos .prt.
        private static List<KeyValuePair<string, int>> ResolveDefaultColorPalette()
        {
            UFSession ufSession = UFSession.GetUFSession();
            var resolved = new List<KeyValuePair<string, int>>();
            foreach (NamedColor named in DefaultColorRgbPalette)
            {
                double[] rgb = new double[3]
                {
                    named.R / 255.0,
                    named.G / 255.0,
                    named.B / 255.0,
                };
                int colorIndex;
                ufSession.Disp.AskClosestColor(
                    UFConstants.UF_DISP_rgb_model, rgb,
                    UFConstants.UF_DISP_CCM_EUCLIDEAN_DISTANCE, out colorIndex);
                resolved.Add(new KeyValuePair<string, int>(named.Name, colorIndex));
            }
            return resolved;
        }

        /// <summary>
        /// Varre todas as faces cilíndricas do componente (via Prototype.Bodies) e agrupa por diâmetro.
        /// Não altera o display — só reconhecimento e agrupamento.
        /// </summary>
        /// <param name="theSession">Sessão NX ativa</param>
        /// <param name="component">Componente (occurrence) a ser varrido</param>
        /// <param name="diameterTolerance">Tolerância em mm para considerar dois diâmetros iguais</param>
        public static List<DiameterGroup> ScanHolesByDiameter(
            Session theSession, Component component, double diameterTolerance = 0.02)
        {
            if (theSession == null) throw new ArgumentNullException(nameof(theSession));
            if (component == null) throw new ArgumentNullException(nameof(component));

            Part protoPart = component.Prototype as Part;
            if (protoPart == null)
                throw new InvalidOperationException("O Prototype do componente não é uma Part válida.");

            var rawFaces = new List<KeyValuePair<double, DisplayableObject>>();

            foreach (Body body in protoPart.Bodies)
            {
                foreach (Face face in body.GetFaces())
                {
                    double diameter;
                    if (TryGetCylindricalDiameter(theSession, face, out diameter))
                    {
                        Face occFace = component.FindOccurrence(face) as Face;
                        rawFaces.Add(new KeyValuePair<double, DisplayableObject>(diameter, occFace ?? face));
                    }
                }
            }

            var groups = new List<DiameterGroup>();
            foreach (var item in rawFaces)
            {
                var existing = groups.FirstOrDefault(g => Math.Abs(g.Diameter - item.Key) <= diameterTolerance);
                if (existing != null)
                    existing.Faces.Add(item.Value);
                else
                    groups.Add(new DiameterGroup { Diameter = item.Key, Faces = new List<DisplayableObject> { item.Value } });
            }

            // Grupos com mais faces primeiro (furos passantes duplicados, padrões repetidos, etc.)
            return groups.OrderByDescending(g => g.Faces.Count).ToList();
        }

        /// <summary>
        /// Varre e aplica uma cor distinta por grupo de diâmetro (fluxo de assembly/Component),
        /// tudo dentro de um único undo mark.
        /// </summary>
        public static List<DiameterGroup> ScanAndColorHolesByDiameter(
            Session theSession,
            Component component,
            double diameterTolerance = 0.02,
            int[] colorPalette = null,
            string undoMarkName = "Colorir furos por diametro")
        {
            var groups = ScanHolesByDiameter(theSession, component, diameterTolerance);
            return ApplyColors(theSession, groups, colorPalette, undoMarkName);
        }

        // ── Sobrecargas em cima de Part direto (SEM assembly/Component) ──
        // Esse é o fluxo mais comum no PATHNC: o restante do código
        // (AUTOMATIC_COUNTERBORE, etc.) sempre trabalha em cima de
        // theSession.Parts.Work, sem passar por Component de assembly. Como
        // não há occurrence pra resolver, a face do Body já É o objeto a
        // colorir - não precisa de FindOccurrence.

        /// <summary>
        /// Varre todas as faces cilíndricas do Part (via Part.Bodies) e agrupa por diâmetro.
        /// Não altera o display — só reconhecimento e agrupamento.
        /// </summary>
        /// <param name="theSession">Sessão NX ativa</param>
        /// <param name="part">Part a ser varrida (ex.: theSession.Parts.Work)</param>
        /// <param name="diameterTolerance">Tolerância em mm para considerar dois diâmetros iguais</param>
        public static List<DiameterGroup> ScanHolesByDiameter(
            Session theSession, Part part, double diameterTolerance = 0.02)
        {
            if (theSession == null) throw new ArgumentNullException(nameof(theSession));
            if (part == null) throw new ArgumentNullException(nameof(part));

            var rawFaces = new List<KeyValuePair<double, DisplayableObject>>();

            foreach (Body body in part.Bodies)
            {
                foreach (Face face in body.GetFaces())
                {
                    double diameter;
                    if (TryGetCylindricalDiameter(theSession, face, out diameter))
                    {
                        rawFaces.Add(new KeyValuePair<double, DisplayableObject>(diameter, face));
                    }
                }
            }

            var groups = new List<DiameterGroup>();
            foreach (var item in rawFaces)
            {
                var existing = groups.FirstOrDefault(g => Math.Abs(g.Diameter - item.Key) <= diameterTolerance);
                if (existing != null)
                    existing.Faces.Add(item.Value);
                else
                    groups.Add(new DiameterGroup { Diameter = item.Key, Faces = new List<DisplayableObject> { item.Value } });
            }

            return groups.OrderByDescending(g => g.Faces.Count).ToList();
        }

        /// <summary>
        /// Varre e aplica uma cor distinta por grupo de diâmetro, direto num Part
        /// (sem assembly/Component) - é a sobrecarga usada pelo botão "Colorir
        /// Furos por Diâmetro" do Form1. Tudo dentro de um único undo mark.
        /// </summary>
        public static List<DiameterGroup> ScanAndColorHolesByDiameter(
            Session theSession,
            Part part,
            double diameterTolerance = 0.02,
            int[] colorPalette = null,
            string undoMarkName = "Colorir furos por diametro")
        {
            var groups = ScanHolesByDiameter(theSession, part, diameterTolerance);
            return ApplyColors(theSession, groups, colorPalette, undoMarkName);
        }

        // ── Versão "auto" (a que o botão do Form1 usa) ──
        // O work part costuma ser uma ASSEMBLY - a geometria real fica no
        // Prototype de um componente carregado (mesmo padrão do journal de
        // referência que já funcionava, que ia em
        // "component1.Prototype.Bodies" pra um componente específico tipo
        // "COMPONENT 01_x_t 1"). workPart.Bodies direto fica VAZIO nesse
        // caso - foi por isso que a primeira versão achou 0 diâmetros.
        //
        // Esse método generaliza isso: em vez de fixar o nome de um
        // componente, percorre TODOS os componentes da assembly
        // recursivamente (assembly dentro de assembly incluso) e junta as
        // faces cilíndricas de todos num único agrupamento por diâmetro -
        // assim furos do mesmo diâmetro em componentes diferentes caem na
        // mesma cor. Se o work part não for assembly (RootComponent nulo),
        // cai pro fluxo direto em cima de workPart.Bodies.
        public static List<DiameterGroup> ScanAndColorHolesByDiameterAuto(
            Session theSession,
            Part workPart,
            double diameterTolerance = 0.02,
            int[] colorPalette = null,
            string undoMarkName = "Colorir furos por diametro")
        {
            if (theSession == null) throw new ArgumentNullException(nameof(theSession));
            if (workPart == null) throw new ArgumentNullException(nameof(workPart));

            var rawFaces = new List<KeyValuePair<double, DisplayableObject>>();

            Component root = workPart.ComponentAssembly.RootComponent;
            if (root != null)
            {
                List<Component> allComponents = new List<Component>();
                CollectComponentsRecursive(root, allComponents);
                foreach (Component comp in allComponents)
                {
                    Part protoPart = comp.Prototype as Part;
                    if (protoPart == null)
                        continue;
                    foreach (Body body in protoPart.Bodies)
                    {
                        foreach (Face face in body.GetFaces())
                        {
                            double diameter;
                            if (TryGetCylindricalDiameter(theSession, face, out diameter))
                            {
                                Face occFace = comp.FindOccurrence(face) as Face;
                                rawFaces.Add(new KeyValuePair<double, DisplayableObject>(diameter, occFace ?? face));
                            }
                        }
                    }
                }
            }
            else
            {
                // Não é assembly - varre direto o work part.
                foreach (Body body in workPart.Bodies)
                {
                    foreach (Face face in body.GetFaces())
                    {
                        double diameter;
                        if (TryGetCylindricalDiameter(theSession, face, out diameter))
                        {
                            rawFaces.Add(new KeyValuePair<double, DisplayableObject>(diameter, face));
                        }
                    }
                }
            }

            var groups = new List<DiameterGroup>();
            foreach (var item in rawFaces)
            {
                var existing = groups.FirstOrDefault(g => Math.Abs(g.Diameter - item.Key) <= diameterTolerance);
                if (existing != null)
                    existing.Faces.Add(item.Value);
                else
                    groups.Add(new DiameterGroup { Diameter = item.Key, Faces = new List<DisplayableObject> { item.Value } });
            }
            groups = groups.OrderByDescending(g => g.Faces.Count).ToList();

            return ApplyColors(theSession, groups, colorPalette, undoMarkName);
        }

        /// <summary>
        /// Desfaz a coloração por diâmetro: varre as mesmas faces cilíndricas
        /// (mesmo critério de ScanAndColorHolesByDiameterAuto - assembly
        /// recursivo com fallback pro Part direto quando não é assembly) e
        /// aplica UMA cor cinza neutra em todas, num único undo mark.
        ///
        /// Não existe "remover cor"/voltar pro estado original na API de
        /// display do NX pra face individual - DisplayModification só sabe
        /// aplicar uma cor, não "desfazer" a atribuição. Na prática,
        /// "descolorir" é isso: forçar tudo pro mesmo cinza neutro, que é
        /// visualmente equivalente a não ter nenhuma cor de QC aplicada.
        /// Retorna quantas faces foram resetadas.
        /// </summary>
        public static int ResetHoleColorsToGray(
            Session theSession,
            Part workPart,
            string undoMarkName = "Descolorir furos (cinza)")
        {
            if (theSession == null) throw new ArgumentNullException(nameof(theSession));
            if (workPart == null) throw new ArgumentNullException(nameof(workPart));

            var faces = new List<DisplayableObject>();

            Component root = workPart.ComponentAssembly.RootComponent;
            if (root != null)
            {
                List<Component> allComponents = new List<Component>();
                CollectComponentsRecursive(root, allComponents);
                foreach (Component comp in allComponents)
                {
                    Part protoPart = comp.Prototype as Part;
                    if (protoPart == null)
                        continue;
                    foreach (Body body in protoPart.Bodies)
                    {
                        foreach (Face face in body.GetFaces())
                        {
                            double diameter;
                            if (TryGetCylindricalDiameter(theSession, face, out diameter))
                            {
                                Face occFace = comp.FindOccurrence(face) as Face;
                                faces.Add(occFace ?? face);
                            }
                        }
                    }
                }
            }
            else
            {
                foreach (Body body in workPart.Bodies)
                {
                    foreach (Face face in body.GetFaces())
                    {
                        double diameter;
                        if (TryGetCylindricalDiameter(theSession, face, out diameter))
                        {
                            faces.Add(face);
                        }
                    }
                }
            }

            if (faces.Count == 0)
                return 0;

            UFSession ufSession = UFSession.GetUFSession();
            double[] rgb = new double[3]
            {
                GrayColor.R / 255.0,
                GrayColor.G / 255.0,
                GrayColor.B / 255.0,
            };
            int grayIndex;
            ufSession.Disp.AskClosestColor(
                UFConstants.UF_DISP_rgb_model, rgb,
                UFConstants.UF_DISP_CCM_EUCLIDEAN_DISTANCE, out grayIndex);

            Session.UndoMarkId markId = theSession.SetUndoMark(Session.MarkVisibility.Invisible, undoMarkName);

            DisplayModification dispMod = theSession.DisplayManager.NewDisplayModification();
            dispMod.ApplyToAllFaces = true;
            dispMod.ApplyToOwningParts = false;
            dispMod.NewColor = grayIndex;
            dispMod.CurveEndPointColorOption = DisplayModification.CurveEndPointColorOptions.NoChange;
            dispMod.Apply(faces.ToArray());
            dispMod.Dispose();

            theSession.UpdateManager.DoUpdate(markId);
            theSession.CleanUpFacetedFacesAndEdges();

            return faces.Count;
        }

        private static void CollectComponentsRecursive(Component comp, List<Component> result)
        {
            if (comp == null)
                return;
            result.Add(comp);
            Component[] children = comp.GetChildren();
            if (children == null)
                return;
            foreach (Component child in children)
                CollectComponentsRecursive(child, result);
        }

        // ── Aplicação de cor compartilhada pelas sobrecargas (Component, Part e Auto) ──
        // Se colorPalette (índices fixos) não for passado, resolve a paleta
        // padrão nomeada (cyan/emerald/purple/yellow/red) pro arquivo atual
        // via ResolveDefaultColorPalette - por isso também guarda o NOME da
        // cor aplicada em cada grupo (AppliedColorName), não só o índice.
        private static List<DiameterGroup> ApplyColors(
            Session theSession, List<DiameterGroup> groups, int[] colorPalette, string undoMarkName)
        {
            if (groups.Count == 0)
                return groups;

            int[] paletteIndices;
            string[] paletteNames;
            if (colorPalette != null)
            {
                // Paleta custom passada pelo chamador - só índices, sem nome.
                paletteIndices = colorPalette;
                paletteNames = null;
            }
            else
            {
                List<KeyValuePair<string, int>> resolved = ResolveDefaultColorPalette();
                paletteIndices = resolved.Select(p => p.Value).ToArray();
                paletteNames = resolved.Select(p => p.Key).ToArray();
            }

            Session.UndoMarkId markId = theSession.SetUndoMark(Session.MarkVisibility.Invisible, undoMarkName);

            for (int i = 0; i < groups.Count; i++)
            {
                int color = paletteIndices[i % paletteIndices.Length];
                string colorName = (paletteNames != null) ? paletteNames[i % paletteNames.Length] : null;

                DisplayModification dispMod = theSession.DisplayManager.NewDisplayModification();
                dispMod.ApplyToAllFaces = true;
                dispMod.ApplyToOwningParts = false;
                dispMod.NewColor = color;
                dispMod.CurveEndPointColorOption = DisplayModification.CurveEndPointColorOptions.NoChange;

                dispMod.Apply(groups[i].Faces.ToArray());
                dispMod.Dispose();

                groups[i].AppliedColor = color;
                groups[i].AppliedColorName = colorName;
            }

            theSession.UpdateManager.DoUpdate(markId);
            theSession.CleanUpFacetedFacesAndEdges();

            return groups;
        }

        /// <summary>
        /// Mede o diâmetro de uma face cilíndrica via Measurement.GetFaceProperties.
        /// Retorna false se a face não for cilíndrica.
        /// </summary>
        public static bool TryGetCylindricalDiameter(Session theSession, Face face, out double diameter)
        {
            diameter = 0.0;
            if (face.SolidFaceType != Face.FaceType.Cylindrical)
                return false;

            ISurface[] faces = new ISurface[] { face };

            double area, perimeter, radius, minRadiusOfCurvature, areaErrorEstimate;
            Point3d cog, anchorPoint;
            bool isApproximate;

            theSession.Measurement.GetFaceProperties(
                faces, 0.99, Measurement.AlternateFace.Radius, true,
                out area, out perimeter, out radius, out cog,
                out minRadiusOfCurvature, out areaErrorEstimate, out anchorPoint, out isApproximate);

            diameter = radius * 2.0;
            return true;
        }

        /// <summary>
        /// Escreve um resumo dos grupos encontrados na Listing Window do NX. Útil pra debug/QC.
        /// </summary>
        public static void LogGroupsToListingWindow(Session theSession, List<DiameterGroup> groups)
        {
            theSession.ListingWindow.Open();
            theSession.ListingWindow.WriteLine(string.Format("Grupos de diametro encontrados: {0}", groups.Count));
            for (int i = 0; i < groups.Count; i++)
            {
                var g = groups[i];
                string corInfo = "";
                if (g.AppliedColor.HasValue)
                {
                    corInfo = " | cor " + g.AppliedColor.Value
                        + (!string.IsNullOrEmpty(g.AppliedColorName) ? " (" + g.AppliedColorName + ")" : "");
                }
                theSession.ListingWindow.WriteLine(string.Format(
                    "  Grupo {0}: D{1:F3} mm | {2} face(s){3}",
                    i + 1, g.Diameter, g.Faces.Count, corInfo));
            }
        }
    }
}
