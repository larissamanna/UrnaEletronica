# Urna Eletrônica: TCP + Multithread (C#)

Aplicação cliente/servidor de uma urna eletrônica. O servidor usa **TCP** (`TcpListener` / `TcpClient`) para gerenciar as conexões e cria **uma thread por cliente** (`System.Threading.Thread`), então atende vários terminais de votação ao mesmo tempo.

> **Origem do código:** o código foi escrito com auxílio de IA (Claude, da Anthropic). A lógica **não foi copiada de nenhum site**. As referências consultadas foram as documentações oficiais da Microsoft sobre [`TcpListener`](https://learn.microsoft.com/dotnet/api/system.net.sockets.tcplistener), [`TcpClient`](https://learn.microsoft.com/dotnet/api/system.net.sockets.tcpclient), [`Thread`](https://learn.microsoft.com/dotnet/api/system.threading.thread) e [`lock`](https://learn.microsoft.com/dotnet/csharp/language-reference/statements/lock).

## Arquivos

| Arquivo | Função |
|---|---|
| `Servidor/Servidor.cs` | Thread principal: abre a porta TCP, aceita conexões, guarda os sockets e cria uma thread por cliente. Também tem o código que cada thread executa |
| `Servidor/ConexaoCliente.cs` | Dados guardados sobre cada cliente conectado (socket, fluxo, endereço, thread...) |
| `Servidor/Urna.cs` | Votos e lista de quem já votou, compartilhados entre as threads e protegidos por `lock` |
| `Servidor/Dados.cs` | Candidatos cadastrados |
| `Cliente/Cliente.cs` | Terminal da urna: conecta via TCP e mostra o menu de votação |

## Como executar

É necessário o .NET SDK 9 ou mais recente.

```bash
# Terminal 1: sobe o servidor (porta padrão 5000)
dotnet run --project Servidor

# Terminais 2, 3, ...: um cliente em cada
dotnet run --project Cliente                   # conecta em 127.0.0.1:5000
dotnet run --project Cliente -- 192.168.0.10   # conecta em outro computador da rede
```

Título de eleitor: **qualquer número** (ex.: 1001, 2000). Cada título vota uma vez só. Candidatos: **10, 20, 30, 40**. Deixar o número vazio vota em BRANCO e qualquer outro número vira voto NULO.

Para encerrar a eleição, aperte **Ctrl+C** no servidor. Ele mostra o resultado final.

## Como funciona

### Conexão TCP
1. `new TcpListener(IPAddress.Any, 5000)` + `Start()` criam o socket de escuta (bind + listen).
2. A thread principal fica num laço chamando `AcceptTcpClient()`, que bloqueia até um cliente conectar e devolve o `TcpClient` (socket exclusivo daquele cliente).
3. A conexão fica aberta durante toda a sessão. Pelo mesmo socket, vários eleitores podem votar em sequência.
4. Ao sair, o cliente fecha o socket (envia FIN). No servidor, `ReadLine()` devolve `null` e a thread fecha a conexão e termina.

### Protocolo
TCP é um **fluxo de bytes**, não de mensagens. Por isso cada mensagem é uma linha de texto terminada em `\n`, e `ReadLine()` junta os bytes até o `\n`. Os campos são separados por `|`.

| Cliente envia | Servidor responde |
|---|---|
| *(ao conectar)* | `BEMVINDO\|mensagem\|10:Ana Souza:Partido Azul;20:...` |
| `IDENTIFICAR\|1001` | `IDENTIFICADO\|1001` ou `ERRO\|motivo` |
| `VOTAR\|10` | `VOTO_OK\|mensagem` ou `ERRO\|motivo` |
| `RESULTADO` | `RESULTADO\|10:Ana Souza:Partido Azul:3;...` |
| `SAIR` | `TCHAU\|mensagem`, depois fecha a conexão |

### Estrutura de dados dos sockets
O servidor guarda os clientes num `Dictionary<int, ConexaoCliente>` (id → objeto do cliente):

```
clientes = {
  1 => ConexaoCliente { Id, Tcp, Fluxo, Leitor, Escritor, Endereco: 127.0.0.1:50663, Thread: 4, Eleitor: "1001", ConectadoEm },
  2 => ConexaoCliente { ... }
}
```
- `Tcp`: o socket TCP do cliente
- `Fluxo` (`NetworkStream`): os bytes que entram e saem pela conexão
- `Leitor` / `Escritor`: convertem bytes em linhas de texto
- `Thread`: a thread que atende esse cliente

O cliente entra no dicionário ao conectar e sai quando a conexão termina. Como várias threads mexem nele, todo acesso usa `lock (travaClientes)`. O servidor imprime essa tabela sempre que ela muda.

### Multithread
- **Thread principal**: só aceita conexões (`AcceptTcpClient`) e cria as threads.
- **Uma thread por cliente** (`new Thread(...)`): cada uma lê e responde **o seu próprio socket**. Quando uma thread fica bloqueada esperando o cliente digitar, as outras continuam atendendo.
- **Concorrência**: os votos são compartilhados. O `lock` da classe `Urna` garante que só uma thread por vez altere a contagem. Assim nenhum voto se perde, e o mesmo eleitor não consegue votar duas vezes, nem por dois terminais ao mesmo tempo.

## Roteiro sugerido para o vídeo
1. Abrir 3 terminais lado a lado.
2. **Terminal 1:** `dotnet run --project Servidor` e mostrar a mensagem "Servidor no ar, escutando em 0.0.0.0:5000".
3. **Terminais 2 e 3:** `dotnet run --project Cliente`. No servidor aparecem as duas conexões, cada uma com IP:porta e **número de thread diferente**.
4. No cliente 1, votar com o título `1001`. No cliente 2, votar com o título `1002`. O servidor mostra o log de cada thread e a apuração parcial.
5. Mostrar o bloqueio: tentar votar de novo com o título `1001` e aparece "já votou".
6. Opção 2 em um cliente para ver a apuração parcial.
7. Sair de um cliente (opção 0). O servidor mostra a conexão encerrada e a thread finalizada.
8. Ctrl+C no servidor para ver o resultado final.
