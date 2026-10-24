/*
Dados de UM cliente conectado

Código escrito com ajuda de IA (Claude)

Cada cliente que conecta vira um objeto desta classe
Esses objetos ficam guardados no dicionário "clientes" do Servidor.cs
*/

using System.Net;
using System.Net.Sockets;
using System.Text;

namespace UrnaServidor;

public class ConexaoCliente
{
    // Número da conexão (1, 2, 3...)
    // É a chave no dicionário
    public int Id { get; }

    // O socket TCP deste cliente: a conexão entre ele e o servidor
    public TcpClient Tcp { get; }

    // O canal por onde os bytes entram e saem
    public NetworkStream Fluxo { get; }

    // Leitor e Escritor transformam bytes em texto e texto em bytes
    // O TCP manda os bytes em sequência, sem separar as mensagens
    // Por isso cada mensagem termina com '\n', e o ReadLine() lê até ele
    public StreamReader Leitor { get; }
    public StreamWriter Escritor { get; }

    // IP e porta do cliente (Ex.: 127.0.0.1:57568)
    public IPEndPoint Endereco { get; }

    // A thread que atende este cliente
    public Thread? Thread { get; set; }

    // Título do eleitor que se identificou (null se ninguém)
    public string? Eleitor { get; set; }

    // Hora em que o cliente conectou
    public DateTime ConectadoEm { get; } = DateTime.Now;

    // Lock para enviar: evita que duas threads escrevam no socket ao mesmo tempo
    // (acontece no Ctrl+C, quando o servidor avisa todos os clientes)
    private readonly object travaEscrita = new();

    public ConexaoCliente(int id, TcpClient tcp)
    {
        Id = id;
        Tcp = tcp;
        Fluxo = tcp.GetStream();
        var utf8 = new UTF8Encoding(false); // UTF-8 para os acentos funcionarem
        Leitor = new StreamReader(Fluxo, utf8);
        Escritor = new StreamWriter(Fluxo, utf8) { AutoFlush = true, NewLine = "\n" };
        Endereco = (IPEndPoint)tcp.Client.RemoteEndPoint!;
    }

    // Envia uma linha de texto para o cliente
    // AutoFlush = true: a mensagem sai pela rede na hora
    public void Enviar(string mensagem)
    {
        lock (travaEscrita)
        {
            Escritor.WriteLine(mensagem);
        }
    }

    // Fecha a conexão TCP com o cliente
    public void Fechar()
    {
        try { Tcp.Close(); } catch { /* já estava fechada */ }
    }
}
