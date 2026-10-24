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
        new(10, "Ana Souza",    "Partido Azul"),
        new(20, "Bruno Lima",   "Partido Verde"),
        new(30, "Carla Mendes", "Partido Amarelo"),
        new(40, "Daniel Rocha", "Partido Vermelho"),
    };
}
