// NX 2406
// THREE_PLUS_TWO_PIPELINE.cs
//
// ORQUESTRADOR fino do pipeline 3+2 Axis - só chama, em sequência, as 3
// classes independentes de estágio:
//   THREE_PLUS_TWO_ROUGH.cs        (Desbaste / Cavity Mill)
//   THREE_PLUS_TWO_REST_MILL.cs    (Rest Mill cutter 16mm + endmill 8mm)
//   THREE_PLUS_TWO_SEMI_FINISH.cs  (Semi-acabamento Zlevel 5 eixos)
// mais o núcleo compartilhado THREE_PLUS_TWO_COMMON.cs (detecção de direção,
// material/ferramenta, preparo de WCS/features, utilidades geométricas).
//
// Esta classe existia antes como um monólito com os 3 estágios em métodos
// separados (RunRoughOnly/RunRestMillOnly/RunSemiFinishOnly). A pedido do
// usuário, cada estágio agora é uma classe própria em arquivo próprio -
// Run/RunWithMaterial aqui viraram só uma chamada em sequência às 3 classes
// novas. Continua existindo (em vez de ser apagada) porque:
//   - é chamada pelo botão "Run All Stages" da página 3+2 Axis (Form1.cs);
//   - é chamada pelo card "3+2 Axis Example" da página Templates (Form1.cs).
// Pra rodar UM estágio de cada vez (sem esperar os outros 2), use os botões
// "1. Rough" / "2. Rest Mill" / "3. Semi-Finish" (que chamam THREE_PLUS_TWO_
// ROUGH.Run / THREE_PLUS_TWO_REST_MILL.Run / THREE_PLUS_TWO_SEMI_FINISH.Run
// diretamente) em vez desta classe.
//
// AVISO (herdado, ainda vale): o acabamento fino de superfícies planas
// (ex-*_PLANAR_SURFACE_FINISH_PLANAR) NÃO foi portado - os arquivos
// originais nunca foram encontrados no projeto, só eram referenciados por
// nome no TEMPLATE_3+2_EXAMPLE_01.cs. Por isso o 3º estágio se chama
// "Semi-Finish" (Zlevel 5 eixos), não "Finish".
public class THREE_PLUS_TWO_PIPELINE
{
    public static void Run(string[] args)
    {





        THREE_PLUS_TWO_ROUGH.Run(args);
        THREE_PLUS_TWO_REST_MILL.Run(args);
        THREE_PLUS_TWO_SEMI_FINISH.Run(args);
    }

    public static int GetUnloadOption(string dummy)
    {
        return (int)NXOpen.Session.LibraryUnloadOption.Immediately;
    }
}
