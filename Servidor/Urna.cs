/*
Votos da eleição (compartilhados entre todas as threads)

Código escrito com ajuda de IA (Claude)

Todas as threads usam o mesmo vetor "votos" e o mesmo conjunto "titulosQueVotaram".
Sem proteção, duas threads mexendo neles ao mesmo tempo dariam erro:
  - Um voto pode se perder: duas threads leem 5, as duas gravam 6.
  - O mesmo eleitor pode votar duas vezes, por dois terminais.

A solução é o lock: só uma thread por vez entra no bloco lock (trava) { ... }.
As outras esperam a vez.
*/

namespace UrnaServidor;

public static class Urna
{
    // A "chave" do lock. Só a thread que está com ela mexe nos votos.
    private static readonly object trava = new();

    // Contagem de votos:
    //   votos[0] a votos[3] = candidatos (mesma ordem de Dados.cs)
    //   votos[4] = BRANCO
    //   votos[5] = NULO
    private static readonly int[] votos = new int[Dados.Candidatos.Length + 2];
    private static readonly int IndiceBranco = Dados.Candidatos.Length;
    private static readonly int IndiceNulo = Dados.Candidatos.Length + 1;

    // Títulos que já votaram. HashSet é um conjunto: não guarda repetidos
    // e responde rápido se um título já está lá.
    private static readonly HashSet<string> titulosQueVotaram = new();

    // Título válido = só números (ex.: 1001, 2000, 123456)
    public static bool TituloValido(string titulo) =>
        titulo.Length > 0 && titulo.All(char.IsDigit);

    // Diz se o título já votou.
    public static bool JaVotou(string titulo)
    {
        lock (trava)
        {
            return titulosQueVotaram.Contains(titulo);
        }
    }

    // Registra o voto. Devolve false se o título já tinha votado.
    public static bool RegistrarVoto(string titulo, string numeroDigitado, out string descricao)
    {
        // Descobre se o voto é em candidato, branco ou nulo.
        int indiceVoto;
        int posicao = Array.FindIndex(Dados.Candidatos, c => c.Numero.ToString() == numeroDigitado);
        if (numeroDigitado == "" || numeroDigitado.Equals("BRANCO", StringComparison.OrdinalIgnoreCase))
        {
            indiceVoto = IndiceBranco;
            descricao = "BRANCO";
        }
        else if (posicao == -1)
        {
            indiceVoto = IndiceNulo;
            descricao = "NULO";
        }
        else
        {
            indiceVoto = posicao;
            var c = Dados.Candidatos[posicao];
            descricao = $"{c.Numero} - {c.Nome} ({c.Partido})";
        }

        // Só uma thread por vez: confere se já votou, marca e soma o voto.
        lock (trava)
        {
            // Add devolve false se o título já estava no conjunto (já votou)
            if (!titulosQueVotaram.Add(titulo))
                return false;

            votos[indiceVoto]++;
        }
        return true;
    }

    // Devolve o resultado atual. Copia os votos dentro do lock
    // para não ler enquanto outra thread está gravando.
    public static List<(string Numero, string Nome, string Partido, int Votos)> Apuracao()
    {
        int[] copia;
        lock (trava)
        {
            copia = (int[])votos.Clone();
        }

        var resultado = new List<(string, string, string, int)>();
        for (int i = 0; i < Dados.Candidatos.Length; i++)
        {
            var c = Dados.Candidatos[i];
            resultado.Add((c.Numero.ToString(), c.Nome, c.Partido, copia[i]));
        }
        resultado.Add(("-", "BRANCO", "-", copia[IndiceBranco]));
        resultado.Add(("-", "NULO", "-", copia[IndiceNulo]));
        return resultado;
    }
}
