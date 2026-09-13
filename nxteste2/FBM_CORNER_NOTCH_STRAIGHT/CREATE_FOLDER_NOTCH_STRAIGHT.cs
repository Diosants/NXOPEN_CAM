using System;
using System.Collections.Generic;
using NXOpen;
using NXOpen.CAM;

namespace NX_3_PLUS_TWO_TOOLPATHS
{
    // ══════════════════════════════════════════════════════════════════
    // CREATE_FOLDER_NOTCH_STRAIGHT
    //
    // Orquestra a estratégia completa do "Entalhe de Canto Reto":
    //   1. Garante que existe uma pasta ("Program Group") no Program Order
    //      View com o nome CORNER_NOTCH_STRAIGHT — cria se não existir,
    //      reaproveita se já existir.
    //   2. Roda o rough (FBM_CORNER_NOTCH_STRAIGHT_ROUGH).
    //   3. [PENDENTE] Move as operações criadas para dentro da pasta.
    //   4. Loga o resultado no ListingWindow.
    //
    // É essa classe que deve ser chamada pelo botão "Aplicar" do card no
    // Strategy Advisor (Form1.cs), ex.:
    //   (s, e) => CREATE_FOLDER_NOTCH_STRAIGHT.Run(null)
    //
    // ══════════════════════════════════════════════════════════════════
    // STATUS DO REPARENT — AÇÃO NECESSÁRIA
    // ══════════════════════════════════════════════════════════════════
    // O CreateProgram abaixo já está corrigido (a assinatura veio do
    // próprio erro do compilador, então é confiável). O método para MOVER
    // uma operação já criada para dentro dessa pasta ainda não foi
    // confirmado — já tentei NCGroupCollection.Reparent e CAMSetup.Reparent
    // e nenhum dos dois existe nessa instalação do NX 2406.
    //
    // Para descobrir o nome certo sem mais chute:
    //   1. NX > Tools > Journal > Record Journal (C#)
    //   2. No Program Order Navigator, arraste manualmente UMA operação
    //      de dentro de NC_PROGRAM para dentro de uma pasta qualquer
    //   3. Tools > Journal > Stop Recording
    //   4. Abra o .cs gerado e procure a linha que faz a movimentação —
    //      normalmente logo após o Session.UndoMarkId ou próximo ao final
    //   5. Cole essa linha aqui no lugar do método ReparentOperations
    //      abaixo (hoje ele só lança NotImplementedException de propósito,
    //      pra você não esquecer de completar isso antes de rodar de vez)
    // ══════════════════════════════════════════════════════════════════
    public class CREATE_FOLDER_NOTCH_STRAIGHT
    {
        // Nome da pasta que vai aparecer no Program Order View do NX.
        private const string FOLDER_NAME = "CORNER_NOTCH_STRAIGHT";

        // Nome do grupo pai onde a pasta nova é criada, caso ainda não exista.
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
                NCGroup folder = GetOrCreateProgramFolder(workPart, FOLDER_NAME, PARENT_PROGRAM_GROUP);

                lw.WriteLine("Pasta '" + FOLDER_NAME + "' pronta no Program Order View.");

                // Roda o rough criando as operações em NC_PROGRAM (padrão
                // atual do FBM_CORNER_NOTCH_STRAIGHT_ROUGH).
                NCGroup ncProgram =
                    (NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject(PARENT_PROGRAM_GROUP);

                List<CAMObject> createdOperations =
                    FBM_CORNER_NOTCH_STRAIGHT_ROUGH.RunAndReturnOperations(workPart, ncProgram);

                lw.WriteLine("Operacoes criadas: " + createdOperations.Count);

                // PENDENTE: mover as operações para dentro da pasta.
                // Descomente a linha abaixo assim que confirmar o método
                // real via Journal (ver instruções no cabeçalho da classe).
                //
                // ReparentOperations(workPart, folder, createdOperations);

                lw.WriteLine(
                    "AVISO: operacoes ainda nao foram movidas para '" + FOLDER_NAME +
                    "' — metodo de reparent pendente de confirmacao (ver Journal).");
            }
            catch (Exception ex)
            {
                lw.WriteLine(ex.ToString());
            }
        }

        // Isolado num método próprio de propósito: assim que você tiver o
        // nome/assinatura certa (via Journal), só troca o corpo daqui —
        // nada mais no arquivo precisa mudar.
        private static void ReparentOperations(Part workPart, NCGroup targetFolder, List<CAMObject> operations)
        {
            throw new NotImplementedException(
                "Complete este método com a chamada real de reparent, obtida gravando um Journal " +
                "(Tools > Journal > Record) enquanto move uma operação manualmente no Program Order Navigator.");
        }

        // Procura a pasta pelo nome; se não existir, cria como Program
        // Group dentro do grupo pai informado (normalmente NC_PROGRAM).
        //
        // Público para poder ser reaproveitado por outras estratégias/wizards
        // que também precisem garantir a mesma pasta antes de criar operações
        // (ex.: WIZARD_CORNER_NOTCH_STRAIGHT, que roda rough + wall + floor
        // finish todos dentro da mesma pasta).
        public static NCGroup GetOrCreateProgramFolder(Part workPart, string folderName, string parentGroupName)
        {
            NCGroup existingFolder = TryFindProgramGroup(workPart, folderName);
            if (existingFolder != null)
                return existingFolder;

            NCGroup parentGroup =
                (NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject(parentGroupName);

            // Assinatura confirmada pelo próprio erro do compilador:
            // CreateProgram(NCGroup parent, string type, string subtype,
            //               NCGroupCollection.UseDefaultName useDefaultName,
            //               string newProgramName)
            CAMObject newFolderObj = workPart.CAMSetup.CAMGroupCollection.CreateProgram(
                parentGroup,
                "mill_planar",
                "PROGRAM",
                NCGroupCollection.UseDefaultName.False,
                folderName);

            NCGroup newFolder = (NCGroup)newFolderObj;

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