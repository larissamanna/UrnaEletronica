/*
Terminal da Urna Eletrônica (cliente TCP)

Código escrito com ajuda de IA (Claude)

COMO EXECUTAR:   dotnet run --project Cliente [ip-do-servidor] [porta]
                 (padrão: 127.0.0.1 e 5000)

O cliente conecta ao servidor uma vez e fica conectado até sair.
Pela mesma conexão, vários eleitores podem votar, um depois do outro.

PROTOCOLO: cada mensagem é uma linha de texto, com os campos separados
por '|'. 
Ex.: "VOTAR|10". O cliente envia um pedido e espera a resposta.
*/

using System.Net.Sockets;
using System.Text;

namespace UrnaCliente;

public static class Cliente
{
    private static StreamReader leitor = null!;
    private static StreamWriter escritor = null!;
    private static List<(string Numero, string Nome, string Partido)> candidatos = new();

    public static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        string host = args.Length > 0 ? args[0] : "127.0.0.1";
        int porta = args.Length > 1 && int.TryParse(args[1], out int p) ? p : 5000;

        // CONECTA AO SERVIDOR
        // new TcpClient(host, porta) cria o socket e já se conecta ao servidor
        TcpClient tcp;
        try
        {
            tcp = new TcpClient(host, porta);
        }
        catch (SocketException)
        {
            Console.WriteLine($"Não foi possível conectar em {host}:{porta}. O servidor está no ar?");
            return;
        }

        // Prepara a leitura e a escrita de texto pela conexão
        // Cada mensagem termina com '\n', e o ReadLine() lê até ele
        var utf8 = new UTF8Encoding(false);
        NetworkStream fluxo = tcp.GetStream();
        leitor = new StreamReader(fluxo, utf8);
        escritor = new StreamWriter(fluxo, utf8) { AutoFlush = true, NewLine = "\n" };

        string[] boasVindas = Receber().Split('|');
        foreach (string item in boasVindas[2].Split(';'))
        {
            string[] campos = item.Split(':');
            candidatos.Add((campos[0], campos[1], campos[2]));
        }

        Console.WriteLine("==============================================");
        Console.WriteLine("           URNA ELETRÔNICA - CLIENTE          ");
        Console.WriteLine("==============================================");
        Console.WriteLine($"Conectado ao servidor {host}:{porta} via TCP");
        Console.WriteLine($"Porta local deste cliente: {((System.Net.IPEndPoint)tcp.Client.LocalEndPoint!).Port}");
        Console.WriteLine(boasVindas[1]);
        Console.WriteLine("Candidatos: " + string.Join(", ", candidatos.Select(c => $"{c.Numero}-{c.Nome}")));
        // MENU (repete até o usuário escolher sair)
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("1 - Votar");
            Console.WriteLine("2 - Ver apuração parcial");
            Console.WriteLine("0 - Sair");
            Console.Write("Escolha: ");
            string opcao = (Console.ReadLine() ?? "0").Trim();

            if (opcao == "1")
            {
                FluxoDeVotacao();
            }
            else if (opcao == "2")
            {
                VerApuracao();
            }
            else if (opcao == "0")
            {
                string[] resposta = Pedir("SAIR");
                Console.WriteLine($">> {resposta[1]}");
                tcp.Close(); // fecha a conexão com o servidor
                return;
            }
            else
            {
                Console.WriteLine("Opção inválida.");
            }
        }
    }

    // Envia um pedido ao servidor e espera a resposta
    private static string[] Pedir(string pedido)
    {
        escritor.WriteLine(pedido);
        return Receber().Split('|');
    }

    // Lê a próxima mensagem do servidor
    // Se o servidor fechar a conexão, ReadLine() devolve null e o programa termina
    private static string Receber()
    {
        string? linha;
        try
        {
            linha = leitor.ReadLine();
        }
        catch (IOException)
        {
            linha = null;
        }

        if (linha is null)
        {
            Console.WriteLine("\nConexão com o servidor encerrada.");
            Environment.Exit(0);
        }
        if (linha.StartsWith("ENCERRADA|"))
        {
            Console.WriteLine($"\n[SERVIDOR] {linha.Split('|')[1]}");
            Environment.Exit(0);
        }
        return linha;
    }

    // Mostra a lista de candidatos na tela
    private static void MostrarCandidatos()
    {
        Console.WriteLine("\n----------- CANDIDATOS -----------");
        foreach (var c in candidatos)
            Console.WriteLine($"  {c.Numero}  {c.Nome,-24} {c.Partido}");
        Console.WriteLine("  (vazio) VOTO EM BRANCO");
        Console.WriteLine("  qualquer outro número = VOTO NULO");
        Console.WriteLine("------------------------------------");
    }

    // VOTAÇÃO: identifica o eleitor, escolhe o candidato, confirma e envia
    private static void FluxoDeVotacao()
    {
        Console.Write("Digite o título de eleitor: ");
        string titulo = (Console.ReadLine() ?? "").Trim();
        string[] ident = Pedir($"IDENTIFICAR|{titulo}");
        if (ident[0] == "ERRO")
        {
            Console.WriteLine($">> {ident[1]}");
            return;
        }

        MostrarCandidatos();

        Console.Write("Digite o número do candidato: ");
        string numero = (Console.ReadLine() ?? "").Trim();
        var escolhido = candidatos.FirstOrDefault(c => c.Numero == numero);
        string descricao = numero == "" ? "VOTO EM BRANCO"
                         : escolhido.Nome is not null ? $"{escolhido.Nome} ({escolhido.Partido})"
                         : "VOTO NULO";

        Console.Write($"Confirma {descricao}? (S/N): ");
        if ((Console.ReadLine() ?? "").Trim().ToUpperInvariant() != "S")
        {
            Console.WriteLine(">> Voto cancelado. Identifique-se novamente para votar.");
            return;
        }

        string[] resposta = Pedir($"VOTAR|{numero}");
        Console.WriteLine($">> {resposta[1]}");
    }

    // Pede o resultado ao servidor e mostra a tabela
    private static void VerApuracao()
    {
        // Resposta: RESULTADO|10:Larissa Manna:Partido Galo:3;...;-:NULO:-:0
        string[] resposta = Pedir("RESULTADO");
        Console.WriteLine("\n----------- APURAÇÃO PARCIAL -----------");
        Console.WriteLine($"  {"NÚMERO",-8}{"CANDIDATO",-24}{"PARTIDO",-20}{"VOTOS"}");
        foreach (string item in resposta[1].Split(';'))
        {
            string[] a = item.Split(':');
            Console.WriteLine($"  {a[0],-8}{a[1],-24}{a[2],-20}{a[3]}");
        }
    }
}
