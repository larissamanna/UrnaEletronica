/*
Candidatos da eleição

Código escrito com ajuda de IA (Claude)

Esta lista nunca muda, então as threads podem ler ao mesmo tempo
sem precisar de lock. O que muda (os votos) fica em Urna.cs.
*/

namespace UrnaServidor;

// "record" é uma classe simples, só para guardar dados
public record Candidato(int Numero, string Nome, string Partido);

public static class Dados
{
    // Candidatos
    // O número é o que o eleitor digita na urna
    public static readonly Candidato[] Candidatos =
    {
        new(10, "Larissa Manna",     "Partido Galo"),
        new(20, "Gabriella Paz",     "Partido Palmeiras"),
        new(30, "Cristiane Santos",  "Partido Maria"),
        new(40, "Lucas Lima",        "Partido America"),
    };
}
