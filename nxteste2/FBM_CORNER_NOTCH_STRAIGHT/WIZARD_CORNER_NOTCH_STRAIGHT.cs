using System;
using System.Collections.Generic;
using NXOpen;
using NXOpen.CAM;
using NX_3_PLUS_TWO_TOOLPATHS;
using FBM_MACHINING_PLANAR_SURFACE;

namespace PATHNC.FBM_CORNER_NOTCH_STRAIGHT
{
    // ══════════════════════════════════════════════════════════════════
    // WIZARD_CORNER_NOTCH_STRAIGHT
    //
    // Estratégia completa do "Entalhe de Canto Reto", com as TRÊS fases
    // organizadas dentro da mesma pasta no Program Order View:
    //   1. Garante a pasta CORNER_NOTCH_STRAIGHT (cria se não existir),
    //      via CreateProgramWithUserName.
    //   2. Roda ROUGH, WALL_FINISH e FLOOR_FINISH, nessa ordem, cada uma
    //      devolvendo a lista de operações criadas.
    //   3. Move todas as operações das três fases para dentro da pasta
    //      de uma vez, via CAMSetup.MoveObjects.
    //   4. Loga o resultado no ListingWindow.
    //
    // É essa classe que deve ser chamada pelo botão "Aplicar" do card no
    // Strategy Advisor (Form1.cs), ex.:
    //   (s, e) => WIZARD_CORNER_NOTCH_STRAIGHT.Run(null)
    // ══════════════════════════════════════════════════════════════════
    public class WIZARD_CORNER_NOTCH_STRAIGHT
    {
        private const string FOLDER_NAME = "CORNER_NOTCH_STRAIGHT";
        private const string PARENT_PROGRAM_GROUP = "NC_PROGRAM";

        public static void Run(string[] args)
        {
            Session theSession = Session.GetSession();
            Part workPart = theSession.Parts.Work;

            if (workPart == null)
                return;

            ListingWindow lw = theSession.ListingWindow;
            lw.Open();

            try
            {
                // 1º passo: garante a pasta ANTES de criar qualquer operação.
                NCGroup folder = GetOrCreateProgramFolder(workPart, FOLDER_NAME, PARENT_PROGRAM_GROUP);
                lw.WriteLine("Pasta '" + FOLDER_NAME + "' pronta.");

                NCGroup ncProgram =
                    (NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject(PARENT_PROGRAM_GROUP);

                // 2º passo: roda as três fases, nessa ordem, todas com
                // operações rastreadas (RunAndReturnOperations).
                List<CAMObject> rough =
                    FBM_CORNER_NOTCH_STRAIGHT_ROUGH.RunAndReturnOperations(workPart, ncProgram);
                lw.WriteLine("Rough: " + rough.Count + " operacao(oes) criada(s).");

                List<CAMObject> wallFinish =
                    FBM_CORNER_NOTCH_STRAIGHT_WALL_FINISH.RunAndReturnOperations(workPart, ncProgram);
                lw.WriteLine("Wall Finish: " + wallFinish.Count + " operacao(oes) criada(s).");

                List<CAMObject> floorFinish =
                    FBM_CORNER_NOTCH_STRAIGHT_FLOOR_FINISH.RunAndReturnOperations(workPart, ncProgram);
                lw.WriteLine("Floor Finish: " + floorFinish.Count + " operacao(oes) criada(s).");

                // 3º passo: junta tudo numa lista só e move para a pasta de uma vez.
                List<CAMObject> todasOperacoes = new List<CAMObject>();
                todasOperacoes.AddRange(rough);
                todasOperacoes.AddRange(wallFinish);
                todasOperacoes.AddRange(floorFinish);

                ReparentOperations(workPart, folder, todasOperacoes);

                lw.WriteLine(
                    "Total: " + todasOperacoes.Count +
                    " operacoes (rough + wall finish + floor finish) organizadas em '" +
                    FOLDER_NAME + "'.");
            }
            catch (Exception ex)
            {
                lw.WriteLine(ex.ToString());
            }
        }

        // Move as operações informadas para dentro da pasta de destino.
        // Método confirmado via Journal gravado no NX: CAMSetup.MoveObjects
        // (não existe Reparent nem em NCGroupCollection nem em CAMSetup
        // nessa instalação — MoveObjects é o real).
        private static void ReparentOperations(Part workPart, NCGroup targetFolder, List<CAMObject> operations)
        {
            if (operations.Count == 0)
                return;

            workPart.CAMSetup.MoveObjects(
                CAMSetup.View.ProgramOrder,
                operations.ToArray(),
                targetFolder,
                CAMSetup.Paste.Inside);
        }

        // Procura a pasta pelo nome; se não existir, cria como Program
        // Group dentro do grupo pai informado (normalmente NC_PROGRAM).
        //
        // Método confirmado via Journal gravado no NX: CreateProgramWithUserName
        // (não CreateProgram, que não existe nessa instalação).
        public static NCGroup GetOrCreateProgramFolder(Part workPart, string folderName, string parentGroupName)
        {
            NCGroup existingFolder = TryFindProgramGroup(workPart, folderName);
            if (existingFolder != null)
                return existingFolder;

            NCGroup parentGroup =
                (NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject(parentGroupName);

            NCGroup newFolder = workPart.CAMSetup.CAMGroupCollection.CreateProgramWithUserName(
                parentGroup,
                "mill_planar",
                "PROGRAM",
                NCGroupCollection.UseDefaultName.False,
                folderName,
                "Program");

            return newFolder;
        }

        // Tenta localizar um Program Group pelo nome. Retorna null se não
        // existir, em vez de deixar a exceção do NXOpen estourar pra fora.
        private static NCGroup TryFindProgramGroup(Part workPart, string name)
        {
            try
            {
                return (NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject(name);
            }
            catch (NXException)
            {
                return null;
            }
        }

        public static void Execute()
        {
            Run(new string[0]);
        }

        public static int GetUnloadOption(string dummy)
        {
            return (int)Session.LibraryUnloadOption.Immediately;
        }
    }
}
