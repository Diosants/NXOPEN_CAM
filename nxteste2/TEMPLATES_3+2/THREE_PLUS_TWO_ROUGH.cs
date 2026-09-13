// NX 2406
// THREE_PLUS_TWO_ROUGH.cs
//
// Estágio de DESBASTE do pipeline 3+2 Axis - a pedido do usuário, NÃO usa a
// detecção dinâmica de direção (THREE_PLUS_TWO_COMMON.cs) pra este botão.
// Em vez disso, chama diretamente as classes existentes de cada direção
// fixa (WCS → Recognize Features → Create Feature Group → Cavity Mill), nas
// 5 direções cardeais (Top/Front/Back/Right/Left), na mesma ordem/lógica do
// TEMPLATE_3+2_EXAMPLE_01.cs original.
//
// CORREÇÃO IMPORTANTE (27/08): eu tinha checado uma cópia local desatualizada
// do projeto e informado, errado, que RIGHT_RECOGNIZE_FEATURE.cs, LEFT_
// RECOGNIZE_FEATURE.cs e as classes de cavity mill de BACK/RIGHT/LEFT não
// existiam. Reconferi direto no projeto (arquivo por arquivo, via listagem
// e leitura no PC) e as 5 direções EXISTEM completas nas 4 etapas:
//   - WCS (WORKPLANES/, namespace NX_3_PLUS_TWO_WCS):
//     TOP/FRONT/BACK/RIGHT/LEFT_VIEW.
//   - RECOGNIZE FEATURES (3+2_RECONGINIZE_FEATURES/, namespace
//     NX_3_PLUS_TWO_RECOGNIZE_FEATURES):
//     TOP/FRONT/BACK/RIGHT/LEFT_RECOGNIZE_FEATURE.
//   - CREATE FEATURE GROUP (3+2_CREATE_FEATURES/, namespace
//     NX_3_PLUS_TWO_CREATE_FEATURE_GROUP):
//     CREATE_TOP/FRONT/BACK/RIGHT/LEFT_FEATURE_GROUP.
//   - CAVITY MILL (3+2_TOOLPATHS/, arquivos CAVITY_MILL_TOP/FRONT/BACK/
//     LEFT/RIGHT.cs): CAVITY_MILL_THREE_PLUS_TWO_TOP/FRONT/BACK/LEFT/RIGHT.
// Por isso as 5 direções entram nas 4 etapas abaixo, exatamente como você
// pediu.
//
// BUG REAL corrigido nas 5 classes de cavity mill (senão o botão quebra com
// o mesmo erro "No object found with this name" que você já viu): o journal
// original de cada uma procurava o grupo "1234" (placeholder de teste
// esquecido) em vez de "NC_PROGRAM" (nome padrão do NX) - corrigi direto nos
// 5 arquivos (CAVITY_MILL_TOP/FRONT/BACK/LEFT/RIGHT.cs).
//
// NÃO mexi em mais nada nesses 5 arquivos - a ferramenta de desbaste
// continua fixa em CUTTER_D40_R1 nas 5 direções (sem o dimensionamento por
// abertura local do sistema dinâmico que fica em THREE_PLUS_TWO_COMMON.cs/
// THREE_PLUS_TWO_ROUGH antigo) - se quiser esse ajuste também, me avisa.
using NX_3_PLUS_TWO_CREATE_FEATURE_GROUP;
using NX_3_PLUS_TWO_RECOGNIZE_FEATURES;
using NX_3_PLUS_TWO_TOOLPATHS;
using NX_3_PLUS_TWO_WCS;

public class THREE_PLUS_TWO_ROUGH
{
    public static void Run(string[] args) { RunWithMaterial(args, null); }

    // 'materialCode' fica aqui só por simetria de assinatura com Rest Mill/
    // Semi-Finish - as classes antigas chamadas abaixo não são parametrizadas
    // por material (RPM/feed fixos, gravados no journal original).
    public static void RunWithMaterial(string[] args, string materialCode)
    {
        // ── WCS ──
        TOP_VIEW.Run(null);
        FRONT_VIEW.Run(null);
        BACK_VIEW.Run(null);
        RIGHT_VIEW.Run(null);
        LEFT_VIEW.Run(null);

        // ── RECOGNIZE FEATURES ──
        TOP_RECOGNIZE_FEATURE.Run(null);
        FRONT_RECOGNIZE_FEATURE.Run(null);
        BACK_RECOGNIZE_FEATURE.Run(null);
        RIGHT_RECOGNIZE_FEATURE.Run(null);
        LEFT_RECOGNIZE_FEATURE.Run(null);

        // ── CREATE FEATURES ──
        CREATE_TOP_FEATURE_GROUP.Run(null);
        CREATE_FRONT_FEATURE_GROUP.Run(null);
        CREATE_BACK_FEATURE_GROUP.Run(null);
        CREATE_RIGHT_FEATURE_GROUP.Run(null);
        CREATE_LEFT_FEATURE_GROUP.Run(null);

        // ── ROUGH STRATEGIES ──
       
        CAVITY_MILL_THREE_PLUS_TWO_FRONT.Run(null);
        CAVITY_MILL_THREE_PLUS_TWO_BACK.Run(null);
        CAVITY_MILL_THREE_PLUS_TWO_RIGHT.Run(null);
        CAVITY_MILL_THREE_PLUS_TWO_LEFT.Run(null);
        CAVITY_MILL_THREE_PLUS_TWO_TOP.Run(null);
    }

    public static int GetUnloadOption(string dummy)
    {
        return (int)NXOpen.Session.LibraryUnloadOption.Immediately;
    }
}
