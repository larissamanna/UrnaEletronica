/*
Servidor TCP multithread da Urna Eletrônica

Código escrito com ajuda de IA (Claude)

COMO EXECUTAR: dotnet run --project Servidor [porta] (porta padrão: 5000)

1) A central espera as urnas se conectarem (thread principal)
2) Cada urna que conecta ganha uma thread nova só para ela
3) Cada thread atende só a sua urna: recebe o voto e confirma
   Enquanto um eleitor pensa, as outras urnas continuam funcionando
4) Todos anotam no mesmo boletim de votos (Urna.cs), um de cada vez (lock)
*/

using System.Net;
using System.Net.Sockets;
using System.Text;

namespace UrnaServidor;

public static class Servidor
{
    // LISTA DE CLIENTES CONECTADOS (estrutura de dados dos sockets)
    // É um dicionário:
    //   chave = número da conexão (1, 2, 3...)
    //   valor = dados do cliente: socket, IP:porta, thread, eleitor...
    // Várias threads mexem nele, então todo acesso usa lock (travaClientes)
    private static readonly Dictionary<int, ConexaoCliente> clientes = new();
    private static readonly object travaClientes = new();

    // Lock do console: evita que duas threads escrevam na tela ao mesmo tempo
    private static readonly object travaConsole = new();

    private static int proximoId = 0;
    private static TcpListener? listener;
    private static volatile bool encerrando = false;

    public static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        int porta = args.Length > 0 && int.TryParse(args[0], out int p) ? p : 5000;

        // ABRE A PORTA DO SERVIDOR
        // IPAddress.Any: aceita conexões de qualquer rede (local, Wi-Fi, cabo)
        // Start(): passa a escutar a porta
        listener = new TcpListener(IPAddress.Any, porta);
        try
        {
            listener.Start();
        }
        catch (SocketException)
        {
            Console.WriteLine($"A porta {porta} já está em uso. Feche o outro servidor ou use outra porta.");
            return;
        }

        Console.WriteLine("==============================================");
        Console.WriteLine("        URNA ELETRÔNICA - SERVIDOR TCP        ");
        Console.WriteLine("==============================================");
        Log($"Servidor no ar, escutando em {IPAddress.Any}:{porta}");
        Log($"Thread principal (aceita conexões): thread {Environment.CurrentManagedThreadId}");
        Log("Candidatos: " + string.Join(", ", Dados.Candidatos.Select(c => $"{c.Numero}-{c.Nome}")));
        Log("Aguardando conexões... (Ctrl+C encerra a eleição e mostra o resultado final)");

        Console.CancelKeyPress += EncerrarEleicao;

        // ESPERA CLIENTES (repete para sempre)
        while (true)
        {
            TcpClient tcp;
            try
            {
                // Fica parado aqui até um cliente conectar
                // Quando conecta, recebemos o socket só daquele cliente
                tcp = listener.AcceptTcpClient();
            }
            catch (SocketException)
            {
                break; // o servidor foi encerrado (Ctrl+C)
            }

            int id = Interlocked.Increment(ref proximoId); // soma 1 de forma segura entre threads (id)
            var conexao = new ConexaoCliente(id, tcp);

            // MULTITHREAD: cria uma thread só para este cliente
            // Ela vai executar o método AtenderCliente
            // IsBackground = true: a thread fecha junto com o programa
            var thread = new Thread(() => AtenderCliente(conexao))
            {
                IsBackground = true,
                Name = $"Cliente-{id}",
            };
            conexao.Thread = thread;

            // Guarda o cliente na lista de conectados
            lock (travaClientes)
            {
                clientes.Add(id, conexao);
            }

            Log($"Nova conexão TCP #{id} vinda de {conexao.Endereco} -> atendida pela thread {thread.ManagedThreadId}");
            MostrarClientes();

            // Liga a thread
            // A thread principal volta para o início do laço
            // E espera o próximo cliente
            thread.Start();
        }
    }

    /*
    ATENDIMENTO DE UM CLIENTE (roda dentro da thread dele)

    Com 3 clientes, este método roda 3 vezes ao mesmo tempo,
    uma em cada thread, cada uma com o seu socket

    PROTOCOLO: cada mensagem é uma linha de texto, com os campos separados por '|'
      Cliente envia        Servidor responde
      (ao conectar)        BEMVINDO|mensagem|lista de candidatos
      IDENTIFICAR|1001     IDENTIFICADO|1001    ou  ERRO|motivo
      VOTAR|10             VOTO_OK|mensagem     ou  ERRO|motivo
      RESULTADO            RESULTADO|votos de cada candidato
      SAIR                 TCHAU|mensagem (e fecha a conexão)
    */
    private static void AtenderCliente(ConexaoCliente c)
    {
        int threadId = Environment.CurrentManagedThreadId;
        string? eleitorAtual = null; // título do eleitor que se identificou neste terminal

        LogThread(c, "Thread iniciada para atender este cliente.");
        try
        {
            // Primeira mensagem: avisa a thread que atende e manda a lista de candidatos
            string listaCandidatos = string.Join(";", Dados.Candidatos.Select(x => $"{x.Numero}:{x.Nome}:{x.Partido}"));
            c.Enviar($"BEMVINDO|Conectado à urna! Você está sendo atendido pela thread {threadId}.|{listaCandidatos}");

            // Espera o cliente mandar uma linha
            // Só esta thread fica parada, as outras continuam atendendo os seus clientes
            // ReadLine() devolve null quando o cliente desconecta
            string? linha;
            while ((linha = c.Leitor.ReadLine()) != null)
            {
                // Separa o comando do valor (Ex.: "VOTAR|10" -> "VOTAR" e "10")
                string[] partes = linha.Split('|');
                string comando = partes[0].Trim().ToUpperInvariant();
                string argumento = partes.Length > 1 ? partes[1].Trim() : "";

                LogThread(c, $"Pedido recebido: {linha}");

                switch (comando)
                {
                    case "IDENTIFICAR":
                        if (!Urna.TituloValido(argumento))
                        {
                            c.Enviar("ERRO|Título inválido. Digite apenas números.");
                        }
                        else if (Urna.JaVotou(argumento))
                        {
                            c.Enviar($"ERRO|O título {argumento} já votou nesta eleição.");
                        }
                        else
                        {
                            eleitorAtual = argumento;
                            lock (travaClientes) { c.Eleitor = argumento; }
                            LogThread(c, $"Eleitor identificado: título {argumento}");
                            MostrarClientes();
                            c.Enviar($"IDENTIFICADO|{argumento}");
                        }
                        break;

                    case "VOTAR":
                        if (eleitorAtual is null)
                        {
                            c.Enviar("ERRO|Identifique-se com o título de eleitor antes de votar.");
                        }
                        else if (!Urna.RegistrarVoto(eleitorAtual, argumento, out string descricao))
                        {
                            // O mesmo eleitor já votou por outro terminal
                            eleitorAtual = null;
                            c.Enviar("ERRO|Este eleitor já votou.");
                        }
                        else
                        {
                            eleitorAtual = null; // libera a urna para o próximo eleitor
                            lock (travaClientes) { c.Eleitor = null; }
                            LogThread(c, $"Voto registrado: {descricao}");
                            MostrarApuracao($"Voto registrado pelo cliente #{c.Id}. Apuração parcial:");
                            c.Enviar($"VOTO_OK|Voto confirmado: {descricao}. FIM!");
                        }
                        break;

                    case "RESULTADO":
                        string apuracao = string.Join(";", Urna.Apuracao().Select(a => $"{a.Numero}:{a.Nome}:{a.Partido}:{a.Votos}"));
                        c.Enviar($"RESULTADO|{apuracao}");
                        break;

                    case "SAIR":
                        c.Enviar("TCHAU|Conexão encerrada. Obrigado!");
                        return; // vai para o finally, que fecha a conexão

                    default:
                        c.Enviar($"ERRO|Comando desconhecido: {comando}");
                        break;
                }
            }
        }
        catch (IOException)
        {
            // O cliente caiu sem avisar (fechou a janela, perdeu a rede)
            LogThread(c, "Conexão perdida com o cliente.");
        }
        catch (ObjectDisposedException)
        {
            // O servidor fechou a conexão (Ctrl+C)
        }
        finally
        {
            // Sempre roda no fim: fecha o socket e tira o cliente da lista
            // Depois disso a thread termina
            c.Fechar();
            lock (travaClientes)
            {
                clientes.Remove(c.Id);
            }
            if (!encerrando)
            {
                Log($"Conexão #{c.Id} ({c.Endereco}) encerrada. Thread {threadId} finalizada.");
                MostrarClientes();
            }
        }
    }

    // ENCERRA A ELEIÇÃO (Ctrl+C): mostra o resultado e desconecta todos
    private static void EncerrarEleicao(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true; // não fecha na hora: antes mostramos o resultado
        encerrando = true;

        Console.WriteLine();
        MostrarApuracao("ELEIÇÃO ENCERRADA. Resultado final:");

        listener?.Stop(); // para de aceitar clientes novos

        // Copia a lista de clientes e avisa cada um que a eleição acabou
        List<ConexaoCliente> abertos;
        lock (travaClientes)
        {
            abertos = clientes.Values.ToList();
        }
        foreach (var c in abertos)
        {
            try { c.Enviar("ENCERRADA|A eleição foi encerrada pelo servidor."); } catch { }
            c.Fechar();
        }

        Environment.Exit(0);
    }

    // MENSAGENS NA TELA DO SERVIDOR
    private static void Log(string mensagem)
    {
        lock (travaConsole)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [SERVIDOR] {mensagem}");
        }
    }

    // Mensagem de uma thread de cliente (mostra o número da thread)
    private static void LogThread(ConexaoCliente c, string mensagem)
    {
        lock (travaConsole)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [THREAD {Environment.CurrentManagedThreadId} | cliente #{c.Id}] {mensagem}");
        }
    }

    // Mostra a tabela de clientes conectados
    private static void MostrarClientes()
    {
        // Copia a lista rápido e solta o lock antes de imprimir
        List<ConexaoCliente> copia;
        lock (travaClientes)
        {
            copia = clientes.Values.OrderBy(c => c.Id).ToList();
        }

        lock (travaConsole)
        {
            if (copia.Count == 0)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [SERVIDOR] Nenhum cliente conectado no momento.");
                return;
            }
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [SERVIDOR] Clientes conectados: {copia.Count}");
            Console.WriteLine($"  {"ID",-4}{"ENDEREÇO",-24}{"THREAD",-8}{"ELEITOR",-20}{"CONECTADO EM"}");
            foreach (var c in copia)
                Console.WriteLine($"  {c.Id,-4}{c.Endereco,-24}{c.Thread?.ManagedThreadId,-8}{c.Eleitor ?? "(não identificado)",-20}{c.ConectadoEm:HH:mm:ss}");
        }
    }

    // Mostra a tabela com os votos de cada candidato
    private static void MostrarApuracao(string titulo)
    {
        var apuracao = Urna.Apuracao();
        lock (travaConsole)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [SERVIDOR] {titulo}");
            Console.WriteLine($"  {"NÚMERO",-8}{"CANDIDATO",-24}{"PARTIDO",-20}{"VOTOS"}");
            foreach (var a in apuracao)
                Console.WriteLine($"  {a.Numero,-8}{a.Nome,-24}{a.Partido,-20}{a.Votos}");
        }
    }
}
