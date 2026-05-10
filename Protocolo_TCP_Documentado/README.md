# Protocolo TCP do Memory River

Este README foi montado para enviar ao grupo. Ele explica o que o protocolo faz, como o fluxo online funciona e coloca os principais codigos diretamente no documento.

## Resumo rapido

O projeto usa duas camadas no modo online:

- **TCP**: cuida do lobby, ou seja, criar sala, entrar por codigo, marcar pronto, avisar saida e entregar o codigo do Relay.
- **Unity Relay/Netcode**: cuida da partida em si, ou seja, cartas, turnos, pontos, combos, vencedor e sincronizacao do tabuleiro.

Na pratica, o TCP nao sincroniza o gameplay. Ele prepara os dois jogadores para entrarem na mesma partida. Quando os dois estao prontos, o host cria uma sessao no Unity Relay e o codigo dessa sessao e enviado ao segundo jogador pelo servidor TCP.

## Stack usada

- Unity 2022.3
- C#
- Servidor TCP em C# console
- Mensagens JSON por linha
- Unity Relay
- Unity Transport
- Netcode for GameObjects

## Como o protocolo funciona

1. O cliente Unity conecta no servidor TCP usando IP e porta.
2. O servidor responde com uma mensagem `welcome` e um `playerId`.
3. O cliente envia `hello` com o nome do jogador.
4. O host envia `create_room`.
5. O servidor cria uma sala e devolve `room_created` com um codigo de 6 caracteres.
6. O segundo jogador envia `join_room` com esse codigo.
7. O servidor coloca os dois na mesma sala e envia `room_state`.
8. Cada jogador envia `ready` quando estiver pronto.
9. Quando os dois estao prontos, o servidor envia `start_match`.
10. O host cria a partida no Unity Relay.
11. O host recebe o `relayJoinCode` e envia isso para o servidor via `relay_info`.
12. O servidor repassa o `relayJoinCode` para o convidado.
13. A partir dai, a partida roda pelo Relay/Netcode.

## Tipos de mensagem

| Mensagem | Quem envia | Para que serve |
| --- | --- | --- |
| `welcome` | Servidor | Entrega o `playerId` ao cliente conectado. |
| `hello` | Cliente | Envia o nome do jogador ao servidor. |
| `create_room` | Host | Pede para criar uma sala. |
| `room_created` | Servidor | Confirma a sala criada e retorna o codigo. |
| `join_room` | Convidado | Pede para entrar em uma sala existente. |
| `room_joined` | Servidor | Confirma que o convidado entrou. |
| `room_state` | Servidor | Atualiza o estado da sala para os dois jogadores. |
| `ready` | Cliente | Marca ou desmarca o jogador como pronto. |
| `start_match` | Servidor | Avisa que os dois estao prontos. |
| `relay_info` | Host/Servidor | Leva o codigo do Unity Relay ate o convidado. |
| `leave_room` | Cliente | Sai da sala. |
| `player_left` | Servidor | Avisa que o outro jogador saiu. |
| `error` | Servidor | Retorna erro de validacao. |

## Exemplos de mensagens JSON

Criar sala:

```json
{"type":"create_room"}
```

Entrar em sala:

```json
{"type":"join_room","roomCode":"ABC123"}
```

Marcar pronto:

```json
{"type":"ready","isReady":true}
```

Enviar codigo do Relay:

```json
{"type":"relay_info","roomCode":"ABC123","relayJoinCode":"XYZ999"}
```

# Codigos principais

Abaixo estao os codigos principais dentro do README. Eles tambem continuam copiados em pastas separadas dentro de `codigos/`.

## Servidor TCP - Program.cs

Este arquivo e o servidor principal. Ele abre a porta TCP, aceita clientes, cria salas, controla pronto, envia atualizacoes e repassa o codigo do Unity Relay.

Arquivo: `ServidorTCP\Program.cs`

```csharp
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TcpLobbyServer
{
    internal class Program
    {
        private static readonly ConcurrentDictionary<string, ClientConnection> Clients = new ConcurrentDictionary<string, ClientConnection>();
        private static readonly ConcurrentDictionary<string, RoomState> Rooms = new ConcurrentDictionary<string, RoomState>();
        private static readonly Random Random = new Random();
        private static TcpListener _listener;

        private static async Task Main(string[] args)
        {
            int port = 7777;
            if (args.Length > 0 && int.TryParse(args[0], out int parsed))
                port = parsed;

            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();

            Console.WriteLine("[TCP] Lobby server started on port " + port);

            while (true)
            {
                TcpClient client = await _listener.AcceptTcpClientAsync();
                _ = HandleClientAsync(client);
            }
        }

        private static async Task HandleClientAsync(TcpClient tcpClient)
        {
            string clientId = Guid.NewGuid().ToString("N");
            Console.WriteLine("[TCP] Client connected: " + clientId);

            NetworkStream stream = tcpClient.GetStream();
            using StreamReader reader = new StreamReader(stream, Encoding.UTF8);
            using StreamWriter writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

            ClientConnection connection = new ClientConnection
            {
                ClientId = clientId,
                TcpClient = tcpClient,
                Writer = writer
            };

            Clients[clientId] = connection;

            await SendAsync(connection, new LobbyMessage
            {
                type = "welcome",
                playerId = clientId
            });

            try
            {
                while (true)
                {
                    string line = await reader.ReadLineAsync();
                    if (line == null)
                        break;

                    LobbyMessage message = LobbyMessage.Deserialize(line);
                    if (message == null)
                        continue;

                    await HandleMessageAsync(connection, message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[TCP] Client error: " + ex.Message);
            }
            finally
            {
                Clients.TryRemove(clientId, out _);
                HandleDisconnect(connection);
                tcpClient.Close();
                Console.WriteLine("[TCP] Client disconnected: " + clientId);
            }
        }

        private static async Task HandleMessageAsync(ClientConnection connection, LobbyMessage message)
        {
            switch (message.type)
            {
                case "hello":
                    connection.PlayerName = message.playerName ?? "Player";
                    break;
                case "create_room":
                    await HandleCreateRoom(connection);
                    break;
                case "join_room":
                    await HandleJoinRoom(connection, message.roomCode);
                    break;
                case "ready":
                    await HandleReady(connection, message.isReady);
                    break;
                case "leave_room":
                    await HandleLeaveRoom(connection);
                    break;
                case "relay_info":
                    await HandleRelayInfo(connection, message.roomCode, message.relayJoinCode);
                    break;
                default:
                    await SendError(connection, "Mensagem desconhecida.");
                    break;
            }
        }

        private static async Task HandleCreateRoom(ClientConnection connection)
        {
            string roomCode = GenerateRoomCode();
            RoomState room = new RoomState
            {
                RoomCode = roomCode,
                HostId = connection.ClientId,
                Status = "waiting"
            };

            Rooms[roomCode] = room;
            connection.RoomCode = roomCode;

            await SendAsync(connection, new LobbyMessage
            {
                type = "room_created",
                room = ToPublicRoom(room)
            });

            Console.WriteLine("[TCP] Room created: " + roomCode);
        }

        private static async Task HandleJoinRoom(ClientConnection connection, string roomCode)
        {
            if (string.IsNullOrWhiteSpace(roomCode))
            {
                await SendError(connection, "Codigo invalido.");
                return;
            }

            roomCode = roomCode.Trim().ToUpperInvariant();

            if (!Rooms.TryGetValue(roomCode, out RoomState room))
            {
                await SendError(connection, "Sala nao encontrada.");
                return;
            }

            if (!string.IsNullOrEmpty(room.GuestId))
            {
                await SendError(connection, "Sala ja esta cheia.");
                return;
            }

            room.GuestId = connection.ClientId;
            room.Status = "ready_check";
            connection.RoomCode = roomCode;

            await SendAsync(connection, new LobbyMessage
            {
                type = "room_joined",
                room = ToPublicRoom(room),
                isHost = false
            });

            await BroadcastRoomState(room);
            Console.WriteLine("[TCP] Guest joined: " + roomCode);
        }

        private static async Task HandleReady(ClientConnection connection, bool isReady)
        {
            if (string.IsNullOrEmpty(connection.RoomCode))
            {
                await SendError(connection, "Voce nao esta em uma sala.");
                return;
            }

            if (!Rooms.TryGetValue(connection.RoomCode, out RoomState room))
            {
                await SendError(connection, "Sala nao encontrada.");
                return;
            }

            if (connection.ClientId == room.HostId)
                room.HostReady = isReady;
            else if (connection.ClientId == room.GuestId)
                room.GuestReady = isReady;

            await BroadcastRoomState(room);

            if (room.HostReady && room.GuestReady)
            {
                room.Status = "starting";
                await BroadcastRoomState(room);
                await Broadcast(room, new LobbyMessage { type = "start_match", room = ToPublicRoom(room) });
            }
        }

        private static async Task HandleRelayInfo(ClientConnection connection, string roomCode, string relayJoinCode)
        {
            if (string.IsNullOrEmpty(roomCode))
                return;

            if (!Rooms.TryGetValue(roomCode, out RoomState room))
                return;

            if (connection.ClientId != room.HostId)
                return;

            room.RelayJoinCode = relayJoinCode ?? "";
            if (string.IsNullOrEmpty(room.GuestId))
                return;

            if (Clients.TryGetValue(room.GuestId, out ClientConnection guest))
            {
                await SendAsync(guest, new LobbyMessage
                {
                    type = "relay_info",
                    relayJoinCode = room.RelayJoinCode,
                    room = ToPublicRoom(room)
                });
            }
        }

        private static async Task HandleLeaveRoom(ClientConnection connection)
        {
            if (string.IsNullOrEmpty(connection.RoomCode))
                return;

            if (!Rooms.TryGetValue(connection.RoomCode, out RoomState room))
                return;

            if (connection.ClientId == room.HostId)
            {
                await NotifyOther(room, connection.ClientId);
                Rooms.TryRemove(room.RoomCode, out _);
                return;
            }

            if (connection.ClientId == room.GuestId)
            {
                room.GuestId = "";
                room.GuestReady = false;
                room.Status = "waiting";
                await NotifyOther(room, connection.ClientId);
                await BroadcastRoomState(room);
            }
        }

        private static void HandleDisconnect(ClientConnection connection)
        {
            if (string.IsNullOrEmpty(connection.RoomCode))
                return;

            if (!Rooms.TryGetValue(connection.RoomCode, out RoomState room))
                return;

            if (connection.ClientId == room.HostId)
            {
                NotifyOther(room, connection.ClientId).GetAwaiter().GetResult();
                Rooms.TryRemove(room.RoomCode, out _);
                return;
            }

            if (connection.ClientId == room.GuestId)
            {
                room.GuestId = "";
                room.GuestReady = false;
                room.Status = "waiting";
                BroadcastRoomState(room).GetAwaiter().GetResult();
                NotifyOther(room, connection.ClientId).GetAwaiter().GetResult();
            }
        }

        private static async Task BroadcastRoomState(RoomState room)
        {
            await Broadcast(room, new LobbyMessage
            {
                type = "room_state",
                room = ToPublicRoom(room)
            });
        }

        private static async Task Broadcast(RoomState room, LobbyMessage message)
        {
            if (!string.IsNullOrEmpty(room.HostId) && Clients.TryGetValue(room.HostId, out ClientConnection host))
                await SendAsync(host, message);

            if (!string.IsNullOrEmpty(room.GuestId) && Clients.TryGetValue(room.GuestId, out ClientConnection guest))
                await SendAsync(guest, message);
        }

        private static async Task NotifyOther(RoomState room, string sourceClientId)
        {
            if (room.HostId != sourceClientId && Clients.TryGetValue(room.HostId, out ClientConnection host))
                await SendAsync(host, new LobbyMessage { type = "player_left", room = ToPublicRoom(room) });

            if (room.GuestId != sourceClientId && Clients.TryGetValue(room.GuestId, out ClientConnection guest))
                await SendAsync(guest, new LobbyMessage { type = "player_left", room = ToPublicRoom(room) });
        }

        private static Task SendError(ClientConnection connection, string error)
        {
            return SendAsync(connection, new LobbyMessage { type = "error", error = error });
        }

        private static Task SendAsync(ClientConnection connection, LobbyMessage message)
        {
            string json = LobbyMessage.Serialize(message);
            return connection.Writer.WriteLineAsync(json);
        }

        private static string GenerateRoomCode()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            string code;
            do
            {
                char[] buffer = new char[6];
                for (int i = 0; i < buffer.Length; i++)
                    buffer[i] = chars[Random.Next(chars.Length)];
                code = new string(buffer);
            } while (Rooms.ContainsKey(code));

            return code;
        }

        private static RoomState ToPublicRoom(RoomState room)
        {
            return new RoomState
            {
                RoomCode = room.RoomCode,
                HostId = room.HostId,
                GuestId = room.GuestId,
                HostReady = room.HostReady,
                GuestReady = room.GuestReady,
                Status = room.Status,
                RelayJoinCode = room.RelayJoinCode
            };
        }
    }

    internal class ClientConnection
    {
        public string ClientId { get; set; }
        public string PlayerName { get; set; }
        public string RoomCode { get; set; }
        public TcpClient TcpClient { get; set; }
        public StreamWriter Writer { get; set; }
    }
}

```

## Servidor TCP - LobbyMessage.cs

Modelo das mensagens JSON no lado do servidor. O campo `type` define qual acao deve ser executada.

Arquivo: `ServidorTCP\LobbyMessage.cs`

```csharp
using System;
using System.Text.Json;

namespace TcpLobbyServer
{
    public class LobbyMessage
    {
        public string type { get; set; } = "";
        public string requestId { get; set; }
        public string roomCode { get; set; }
        public string playerId { get; set; }
        public string playerName { get; set; }
        public bool isHost { get; set; }
        public bool isReady { get; set; }
        public string status { get; set; }
        public string error { get; set; }
        public string relayJoinCode { get; set; }
        public RoomState room { get; set; }

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        public static string Serialize(LobbyMessage message)
        {
            return JsonSerializer.Serialize(message, JsonOptions);
        }

        public static LobbyMessage Deserialize(string json)
        {
            return JsonSerializer.Deserialize<LobbyMessage>(json, JsonOptions);
        }
    }
}

```

## Servidor TCP - RoomState.cs

Modelo do estado da sala no servidor. Guarda codigo, host, convidado, prontos, status e codigo do Relay.

Arquivo: `ServidorTCP\RoomState.cs`

```csharp
using System;

namespace TcpLobbyServer
{
    public class RoomState
    {
        public string RoomCode { get; set; } = "";
        public string HostId { get; set; } = "";
        public string GuestId { get; set; } = "";
        public bool HostReady { get; set; }
        public bool GuestReady { get; set; }
        public string Status { get; set; } = "waiting";
        public string RelayJoinCode { get; set; } = "";
    }
}

```

## Unity TCP - TcpLobbyClient.cs

Cliente TCP usado dentro da Unity. Ele conecta no servidor, envia mensagens JSON e escuta respostas sem travar a thread principal da Unity.

Arquivo: `Unity\TcpLobby\TcpLobbyClient.cs`

```csharp
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace TcpLobby
{
    public class TcpLobbyClient : MonoBehaviour
    {
        public string serverIp = "127.0.0.1";
        public int serverPort = 7777;
        public string playerName = "Player";

        public bool IsConnected { get; private set; }
        public string PlayerId { get; private set; } = "";

        public event Action Connected;
        public event Action Disconnected;
        public event Action<LobbyMessage> MessageReceived;
        public event Action<string> Log;

        private TcpClient _client;
        private StreamReader _reader;
        private StreamWriter _writer;
        private CancellationTokenSource _cts;
        private readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();

        private void Update()
        {
            while (_mainThreadQueue.TryDequeue(out Action action))
                action?.Invoke();
        }

        public async Task<bool> ConnectAsync()
        {
            if (IsConnected)
                return true;

            try
            {
                _client = new TcpClient();
                await _client.ConnectAsync(serverIp, serverPort);

                NetworkStream stream = _client.GetStream();
                _reader = new StreamReader(stream, Encoding.UTF8);
                _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

                _cts = new CancellationTokenSource();
                _ = Task.Run(() => ReadLoopAsync(_cts.Token));

                IsConnected = true;
                EnqueueMainThread(() => Connected?.Invoke());

                await SendAsync(new LobbyMessage
                {
                    type = "hello",
                    playerName = playerName
                });

                LogInfo("TCP connected to lobby server.");
                return true;
            }
            catch (Exception ex)
            {
                LogInfo("TCP connect failed: " + ex.Message);
                return false;
            }
        }

        public async Task DisconnectAsync()
        {
            if (!IsConnected)
                return;

            try
            {
                await SendAsync(new LobbyMessage { type = "leave_room" });
            }
            catch
            {
                // ignore on shutdown
            }

            Cleanup();
        }

        public Task SendAsync(LobbyMessage message)
        {
            if (_writer == null)
                return Task.CompletedTask;

            string json = LobbyMessage.Serialize(message);
            return _writer.WriteLineAsync(json);
        }

        public Task CreateRoomAsync()
        {
            return SendAsync(new LobbyMessage { type = "create_room" });
        }

        public Task JoinRoomAsync(string roomCode)
        {
            return SendAsync(new LobbyMessage { type = "join_room", roomCode = roomCode });
        }

        public Task SetReadyAsync(bool isReady)
        {
            return SendAsync(new LobbyMessage { type = "ready", isReady = isReady });
        }

        public Task SendRelayInfoAsync(string roomCode, string relayJoinCode)
        {
            return SendAsync(new LobbyMessage
            {
                type = "relay_info",
                roomCode = roomCode,
                relayJoinCode = relayJoinCode
            });
        }

        private async Task ReadLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    string line = await _reader.ReadLineAsync();
                    if (line == null)
                        break;

                    LobbyMessage message = LobbyMessage.Deserialize(line);
                    if (message == null)
                        continue;

                    if (message.type == "welcome" && !string.IsNullOrEmpty(message.playerId))
                        PlayerId = message.playerId;

                    EnqueueMainThread(() => MessageReceived?.Invoke(message));
                }
            }
            catch (Exception ex)
            {
                LogInfo("TCP read loop stopped: " + ex.Message);
            }
            finally
            {
                EnqueueMainThread(() => Disconnected?.Invoke());
                Cleanup();
            }
        }

        private void Cleanup()
        {
            IsConnected = false;
            _cts?.Cancel();
            _cts = null;

            try { _writer?.Dispose(); } catch { }
            try { _reader?.Dispose(); } catch { }
            try { _client?.Close(); } catch { }

            _writer = null;
            _reader = null;
            _client = null;
        }

        private void EnqueueMainThread(Action action)
        {
            _mainThreadQueue.Enqueue(action);
        }

        private void LogInfo(string message)
        {
            EnqueueMainThread(() => Log?.Invoke(message));
            Debug.Log("[TcpLobbyClient] " + message);
        }
    }
}

```

## Unity TCP - TcpLobbyManager.cs

Gerenciador que liga a UI ao cliente TCP. Ele cria sala, entra em sala, marca pronto e inicia o handshake com o Relay quando recebe `start_match`.

Arquivo: `Unity\TcpLobby\TcpLobbyManager.cs`

```csharp
using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace TcpLobby
{
    public class TcpLobbyManager : MonoBehaviour
    {
        public TcpLobbyClient client;
        public RelayMatchController relayController;

        public Text roomCodeText;
        public Text statusText;
        public Button readyButton;
        public Button createRoomButton;
        public Button joinRoomButton;
        public InputField joinCodeInput;

        public bool autoConnectOnStart = true;

        private RoomState _roomState;
        private bool _isHost;
        private bool _isReady;

        public event Action<string> RoomCodeChanged;
        public event Action<string> StatusChanged;

        private void Awake()
        {
            if (client == null)
                client = GetComponent<TcpLobbyClient>();

            if (relayController == null)
                relayController = RelayMatchController.EnsureInstance();

            if (client != null)
            {
                client.Connected += HandleConnected;
                client.Disconnected += HandleDisconnected;
                client.MessageReceived += HandleMessage;
                client.Log += HandleLog;
            }
        }

        private async void Start()
        {
            if (autoConnectOnStart && client != null)
                await client.ConnectAsync();
        }

        private void OnDestroy()
        {
            if (client == null)
                return;

            client.Connected -= HandleConnected;
            client.Disconnected -= HandleDisconnected;
            client.MessageReceived -= HandleMessage;
            client.Log -= HandleLog;
        }

        public async void CreateRoom()
        {
            if (client == null || !client.IsConnected)
            {
                PublishStatus("Servidor TCP nao conectado.");
                return;
            }

            _isHost = true;
            _isReady = false;
            await client.CreateRoomAsync();
            PublishStatus("Criando sala...");
        }

        public async void JoinRoom()
        {
            if (client == null || !client.IsConnected)
            {
                PublishStatus("Servidor TCP nao conectado.");
                return;
            }

            string code = joinCodeInput != null ? joinCodeInput.text.Trim().ToUpperInvariant() : "";
            if (string.IsNullOrEmpty(code))
            {
                PublishStatus("Informe um codigo valido.");
                return;
            }

            _isHost = false;
            _isReady = false;
            await client.JoinRoomAsync(code);
            PublishStatus("Entrando na sala...");
        }

        public async void ToggleReady()
        {
            if (client == null || !client.IsConnected)
                return;

            _isReady = !_isReady;
            await client.SetReadyAsync(_isReady);
            UpdateReadyButton();
        }

        public void LeaveRoom()
        {
            _roomState = null;
            _isReady = false;
            _isHost = false;
            PublishRoomCode("");
            PublishStatus("Sala encerrada.");
        }

        private void HandleConnected()
        {
            PublishStatus("Conectado ao servidor TCP.");
        }

        private void HandleDisconnected()
        {
            PublishStatus("Conexao TCP encerrada.");
            _roomState = null;
            _isReady = false;
            UpdateReadyButton();
        }

        private void HandleMessage(LobbyMessage message)
        {
            switch (message.type)
            {
                case "room_created":
                    _roomState = message.room;
                    _isHost = true;
                    PublishRoomCode(_roomState != null ? _roomState.roomCode : "");
                    PublishStatus("Sala criada. Convide o outro jogador.");
                    break;
                case "room_joined":
                    _roomState = message.room;
                    _isHost = message.isHost;
                    PublishRoomCode(_roomState != null ? _roomState.roomCode : "");
                    PublishStatus("Entrou na sala. Marque pronto.");
                    break;
                case "room_state":
                    _roomState = message.room;
                    PublishRoomCode(_roomState != null ? _roomState.roomCode : "");
                    PublishStatus("Sala atualizada.");
                    break;
                case "start_match":
                    PublishStatus("Ambos prontos. Iniciando partida...");
                    BeginRelayHandshake();
                    break;
                case "relay_info":
                    if (!string.IsNullOrEmpty(message.relayJoinCode))
                    {
                        PublishStatus("Recebido codigo do Relay. Entrando...");
                        _ = relayController.JoinMatchAsync(message.relayJoinCode);
                    }
                    break;
                case "player_left":
                    PublishStatus("O outro jogador saiu da sala.");
                    _roomState = null;
                    _isReady = false;
                    UpdateReadyButton();
                    break;
                case "error":
                    PublishStatus(message.error ?? "Erro desconhecido.");
                    break;
            }
        }

        private async void BeginRelayHandshake()
        {
            if (_isHost)
            {
                bool created = await relayController.CreateMatchAsync();
                if (!created)
                {
                    PublishStatus("Falha ao iniciar Relay.");
                    return;
                }

                string joinCode = relayController.CurrentJoinCode;
                if (!string.IsNullOrEmpty(joinCode) && _roomState != null)
                    await client.SendRelayInfoAsync(_roomState.roomCode, joinCode);
            }
        }

        private void PublishRoomCode(string value)
        {
            if (roomCodeText != null)
            {
                roomCodeText.text = value;
                roomCodeText.gameObject.SetActive(!string.IsNullOrEmpty(value));
            }

            RoomCodeChanged?.Invoke(value);
        }

        private void PublishStatus(string message)
        {
            if (statusText != null)
                statusText.text = message;

            StatusChanged?.Invoke(message);
            Debug.Log("[TcpLobbyManager] " + message);
        }

        private void UpdateReadyButton()
        {
            if (readyButton == null)
                return;

            Text label = readyButton.GetComponentInChildren<Text>();
            if (label != null)
                label.text = _isReady ? "Pronto!" : "Marcar pronto";
        }

        private void HandleLog(string message)
        {
            Debug.Log("[TcpLobby] " + message);
        }
    }
}

```

## Unity TCP - LobbyMessage.cs

Modelo das mensagens JSON no lado da Unity. Precisa ter os mesmos campos esperados pelo servidor.

Arquivo: `Unity\TcpLobby\LobbyMessage.cs`

```csharp
using System;
using UnityEngine;

namespace TcpLobby
{
    [Serializable]
    public class LobbyMessage
    {
        public string type;
        public string requestId;
        public string roomCode;
        public string playerId;
        public string playerName;
        public bool isHost;
        public bool isReady;
        public string status;
        public string error;
        public string relayJoinCode;
        public RoomState room;

        public static string Serialize(LobbyMessage message)
        {
            return JsonUtility.ToJson(message);
        }

        public static LobbyMessage Deserialize(string json)
        {
            return JsonUtility.FromJson<LobbyMessage>(json);
        }
    }
}

```

## Unity TCP - RoomState.cs

Modelo do estado da sala recebido pela Unity.

Arquivo: `Unity\TcpLobby\RoomState.cs`

```csharp
using System;

namespace TcpLobby
{
    [Serializable]
    public class RoomState
    {
        public string roomCode;
        public string hostId;
        public string guestId;
        public bool hostReady;
        public bool guestReady;
        public string status;
    }
}

```

## Unity Multiplayer - GameLaunchConfig.cs

Classe estatica que guarda o modo de jogo e o codigo da sala entre cenas.

Arquivo: `Unity\Multiplayer\GameLaunchConfig.cs`

```csharp
public enum GameLaunchMode
{
    Story,
    CreateMatch,
    JoinMatch
}

public static class GameLaunchConfig
{
    public static GameLaunchMode CurrentMode { get; private set; } = GameLaunchMode.Story;
    public static string RoomCode { get; private set; } = "";
    public static int StoryChapter { get; private set; } = 1;
    public static string PendingMenuStatus { get; private set; } = "";

    public static bool IsOnlineMode
    {
        get { return CurrentMode == GameLaunchMode.CreateMatch || CurrentMode == GameLaunchMode.JoinMatch; }
    }

    public static void ConfigureStory(int storyChapter = 1)
    {
        CurrentMode = GameLaunchMode.Story;
        RoomCode = "";
        StoryChapter = storyChapter < 1 ? 1 : storyChapter;
        PendingMenuStatus = "";
    }

    public static void ConfigureCreateMatch(string roomCode)
    {
        CurrentMode = GameLaunchMode.CreateMatch;
        RoomCode = roomCode ?? "";
        StoryChapter = 1;
        PendingMenuStatus = "";
    }

    public static void ConfigureJoinMatch(string roomCode)
    {
        CurrentMode = GameLaunchMode.JoinMatch;
        RoomCode = roomCode ?? "";
        StoryChapter = 1;
        PendingMenuStatus = "";
    }

    public static void SetPendingMenuStatus(string message)
    {
        PendingMenuStatus = message ?? "";
    }

    public static string ConsumePendingMenuStatus()
    {
        string current = PendingMenuStatus;
        PendingMenuStatus = "";
        return current;
    }
}

```

## Unity Menu - MainMenuController.cs

Integracao do menu. Quando `useTcpLobby` esta ativo, os botoes de criar e encontrar partida chamam o lobby TCP.

Arquivo: `Unity\Multiplayer\MainMenuController.cs`

```csharp
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    public string storySceneName = SceneIds.StoryMenu;
    public string multiplayerSceneName = SceneIds.Gameplay;

    public GameObject mainPanel;
    public GameObject createMatchPanel;
    public GameObject joinMatchPanel;

    public Text roomCodeText;
    public Text statusText;
    public InputField joinCodeInput;

    private string _currentRoomCode = "";
    private RelayMatchController _relayController;
    public bool useTcpLobby = false;
    public TcpLobby.TcpLobbyManager tcpLobbyManager;

    private void Awake()
    {
        _relayController = RelayMatchController.EnsureInstance();
        _relayController.RoomCodeChanged += HandleRoomCodeChanged;
        _relayController.StatusChanged += HandleStatusChanged;

        if (tcpLobbyManager == null)
            tcpLobbyManager = FindObjectOfType<TcpLobby.TcpLobbyManager>();

        BindUI();
        ResetTransientUI();
        ForceMainPanelState();
    }

    private void Start()
    {
        ForceMainPanelState();
    }

    private void OnDestroy()
    {
        if (_relayController == null)
            return;

        _relayController.RoomCodeChanged -= HandleRoomCodeChanged;
        _relayController.StatusChanged -= HandleStatusChanged;
    }

    public void StartStoryMode()
    {
        if (_relayController != null)
            _relayController.CancelPendingSessionInMenu();

        GameLaunchConfig.ConfigureStory(1);
        SceneManager.LoadScene(storySceneName);
    }

    public void OpenCreateMatchPanel()
    {
        _currentRoomCode = "";
        SetRoomCodeDisplay("");
        ShowOnly(createMatchPanel);
        SetStatus("Clique em Criar Sala para gerar o codigo.");
    }

    public async void ConfirmCreateMatch()
    {
        if (useTcpLobby && tcpLobbyManager != null)
        {
            tcpLobbyManager.CreateRoom();
            return;
        }

        if (_relayController == null || _relayController.IsBusy)
            return;

        SetStatus("Criando sala...");

        bool created = await _relayController.CreateMatchAsync();
        if (!created)
            return;

        if (!string.IsNullOrEmpty(_relayController.CurrentJoinCode))
        {
            _currentRoomCode = _relayController.CurrentJoinCode;
            SetRoomCodeDisplay(_currentRoomCode);
        }
    }

    public void OpenJoinMatchPanel()
    {
        if (joinCodeInput != null)
            joinCodeInput.text = "";

        ShowOnly(joinMatchPanel);
        SetStatus("Digite o codigo da sala para entrar.");
    }

    public async void ConfirmJoinMatch()
    {
        if (useTcpLobby && tcpLobbyManager != null)
        {
            tcpLobbyManager.JoinRoom();
            return;
        }

        if (_relayController == null || _relayController.IsBusy)
            return;

        string roomCode = joinCodeInput != null ? joinCodeInput.text.Trim().ToUpperInvariant() : "";
        if (string.IsNullOrEmpty(roomCode))
        {
            SetStatus("Informe um codigo de partida valido.");
            return;
        }

        SetStatus("Entrando na sala...");
        await _relayController.JoinMatchAsync(roomCode);
    }

    public void ShowMainPanel()
    {
        if (_relayController != null)
            _relayController.CancelPendingSessionInMenu();

        if (useTcpLobby && tcpLobbyManager != null)
            tcpLobbyManager.LeaveRoom();

        ResetTransientUI();
        ShowOnly(mainPanel);
        ApplyInitialStatus();
    }

    public void ExitGame()
    {
        Application.Quit();
    }

    private void ShowOnly(GameObject activePanel)
    {
        if (mainPanel != null)
            mainPanel.SetActive(activePanel == mainPanel);

        if (createMatchPanel != null)
            createMatchPanel.SetActive(activePanel == createMatchPanel);

        if (joinMatchPanel != null)
            joinMatchPanel.SetActive(activePanel == joinMatchPanel);
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }

    private void BindUI()
    {
        mainPanel = FindObjectByName("MainPanel");
        createMatchPanel = FindObjectByName("CreateMatchPanel");
        joinMatchPanel = FindObjectByName("JoinMatchPanel");
        roomCodeText = FindText("RoomCode");
        statusText = FindText("StatusText");
        joinCodeInput = FindInputField("JoinCodeInput");

        BindButton("Modo HistoriaButton", StartStoryMode);
        BindButton("Criar PartidaButton", OpenCreateMatchPanel);
        BindButton("Encontrar PartidaButton", OpenJoinMatchPanel);
        BindButton("SairButton", ExitGame);
        BindButton("Criar SalaButton", ConfirmCreateMatch);
        BindButton("Encontrar SalaButton", ConfirmJoinMatch);

        if (createMatchPanel != null)
            BindButtonInside(createMatchPanel.transform, "VoltarButton", ShowMainPanel);

        if (joinMatchPanel != null)
            BindButtonInside(joinMatchPanel.transform, "VoltarButton", ShowMainPanel);
    }

    private void ResetTransientUI()
    {
        _currentRoomCode = "";
        SetRoomCodeDisplay("");

        if (joinCodeInput != null)
            joinCodeInput.text = "";
    }

    private void ForceMainPanelState()
    {
        if (mainPanel == null || createMatchPanel == null || joinMatchPanel == null)
            BindUI();

        ResetTransientUI();
        ShowOnly(mainPanel);
        ApplyInitialStatus();
    }

    private void SetRoomCodeDisplay(string value)
    {
        if (roomCodeText == null)
            return;

        roomCodeText.text = value;
        roomCodeText.gameObject.SetActive(!string.IsNullOrEmpty(value));
    }

    private void ApplyInitialStatus()
    {
        string pendingStatus = GameLaunchConfig.ConsumePendingMenuStatus();
        if (!string.IsNullOrEmpty(pendingStatus))
        {
            SetStatus(pendingStatus);
            return;
        }

        if (_relayController != null && !string.IsNullOrEmpty(_relayController.CurrentStatusMessage))
        {
            SetStatus(_relayController.CurrentStatusMessage);
            return;
        }

        SetStatus("Escolha um modo de jogo.");
    }

    private void HandleRoomCodeChanged(string roomCode)
    {
        if (createMatchPanel == null || !createMatchPanel.activeSelf)
            return;

        _currentRoomCode = roomCode ?? "";
        SetRoomCodeDisplay(_currentRoomCode);
    }

    private void HandleStatusChanged(string statusMessage)
    {
        if (string.IsNullOrEmpty(statusMessage))
            return;

        SetStatus(statusMessage);
    }

    private void BindButton(string objectName, UnityEngine.Events.UnityAction action)
    {
        Button button = FindButton(objectName);
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private void BindButtonInside(Transform scope, string objectName, UnityEngine.Events.UnityAction action)
    {
        if (scope == null)
            return;

        Transform target = FindDeepChild(scope, objectName);
        if (target == null)
            return;

        Button button = target.GetComponent<Button>();
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private Button FindButton(string objectName)
    {
        Transform target = FindDeepChild(transform, objectName);
        return target != null ? target.GetComponent<Button>() : null;
    }

    private Text FindText(string objectName)
    {
        Transform target = FindDeepChild(transform, objectName);
        return target != null ? target.GetComponent<Text>() : null;
    }

    private InputField FindInputField(string objectName)
    {
        Transform target = FindDeepChild(transform, objectName);
        return target != null ? target.GetComponent<InputField>() : null;
    }

    private GameObject FindObjectByName(string objectName)
    {
        Transform target = FindDeepChild(transform, objectName);
        return target != null ? target.gameObject : null;
    }

    private Transform FindDeepChild(Transform parent, string objectName)
    {
        if (parent.name == objectName)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeepChild(parent.GetChild(i), objectName);
            if (found != null)
                return found;
        }

        return null;
    }
}

```

## Trechos importantes do RelayMatchController.cs

O arquivo `RelayMatchController.cs` e grande porque controla o multiplayer inteiro da partida. Para o protocolo TCP, as partes mais importantes sao estas:

### Criar partida no Relay

```csharp
public async Task<bool> CreateMatchAsync()
{
    await EnsureServicesInitializedAsync();

    Allocation allocation = await RelayService.Instance.CreateAllocationAsync(1);
    string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

    _transport.SetRelayServerData(new RelayServerData(allocation, "dtls"));

    if (!_networkManager.StartHost())
        return false;

    RegisterMessageHandlers();
    CurrentJoinCode = joinCode;
    GameLaunchConfig.ConfigureCreateMatch(joinCode);
    PublishRoomCode(joinCode);
    return true;
}
```

Essa funcao e chamada pelo host depois que o TCP envia `start_match`. Ela cria a sala real no Unity Relay e gera o codigo que sera repassado ao convidado.

### Entrar na partida pelo codigo do Relay

```csharp
public async Task<bool> JoinMatchAsync(string joinCode)
{
    await EnsureServicesInitializedAsync();

    JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
    _transport.SetRelayServerData(new RelayServerData(joinAllocation, "dtls"));

    if (!_networkManager.StartClient())
        return false;

    RegisterMessageHandlers();
    CurrentJoinCode = joinCode.ToUpperInvariant();
    GameLaunchConfig.ConfigureJoinMatch(CurrentJoinCode);
    return true;
}
```

Essa funcao e chamada pelo convidado depois que ele recebe `relay_info` pelo TCP.

### Mensagens internas do gameplay

```csharp
private const string StateSyncMessage = "memory.state.sync";
private const string FlipRequestMessage = "memory.flip.request";
private const string ReturnToMenuMessage = "memory.return.menu";
private const string GameplayReadyMessage = "memory.gameplay.ready";
private const string RematchRequestMessage = "memory.rematch.request";
```

Essas mensagens nao sao do TCP. Elas sao mensagens internas do Netcode usadas depois que a partida ja comecou.

## Unity Multiplayer - RelayMatchController.cs completo

Este e o codigo completo que assume a partida depois que o lobby TCP termina. Ele cria/entra no Unity Relay, registra mensagens do Netcode, controla tabuleiro, turno, pontuacao, combo, fim de jogo e revanche.

Arquivo: `Unity\Multiplayer\RelayMatchController.cs`

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;
using LoadSceneMode = UnityEngine.SceneManagement.LoadSceneMode;

public class RelayMatchController : MonoBehaviour
{
    private const string StateSyncMessage = "memory.state.sync";
    private const string FlipRequestMessage = "memory.flip.request";
    private const string ReturnToMenuMessage = "memory.return.menu";
    private const string GameplayReadyMessage = "memory.gameplay.ready";
    private const string RematchRequestMessage = "memory.rematch.request";

    private static RelayMatchController _instance;

    private NetworkManager _networkManager;
    private UnityTransport _transport;
    private GameManager _gameManager;

    private bool _networkCallbacksRegistered;
    private bool _messageHandlersRegistered;
    private bool _servicesInitialized;
    private bool _manualShutdown;
    private bool _returningToMenu;
    private bool _gameStarted;
    private bool _boardInitialized;
    private bool _previewRoutineStarted;
    private bool _previewRunning;
    private bool _turnLocked;
    private bool _gameOver;
    private bool _hostGameplayReady;
    private bool _guestGameplayReady;

    private int[] _boardValues = Array.Empty<int>();
    private int[] _boardStates = Array.Empty<int>();
    private readonly int[] _scores = new int[2];
    private readonly int[] _comboStreaks = new int[2];
    private int _lastScoreEventSlot = -1;
    private int _lastPointsEarned;
    private int _lastComboValue;
    private int _currentTurnSlot;
    private int _firstSelectionIndex = -1;
    private ulong _hostClientId = ulong.MaxValue;
    private ulong _guestClientId = ulong.MaxValue;

    private bool _hasSnapshot;
    private SnapshotState _latestSnapshot;

    public static RelayMatchController Instance
    {
        get { return EnsureInstance(); }
    }

    public string CurrentJoinCode { get; private set; } = "";
    public string CurrentStatusMessage { get; private set; } = "";
    public bool IsBusy { get; private set; }

    public bool SessionRunning
    {
        get { return _networkManager != null && _networkManager.IsListening; }
    }

    public event Action<string> RoomCodeChanged;
    public event Action<string> StatusChanged;
    public event Action<bool> BusyStateChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBootstrap()
    {
        EnsureInstance();
    }

    public static RelayMatchController EnsureInstance()
    {
        if (_instance != null)
            return _instance;

        RelayMatchController existing = FindObjectOfType<RelayMatchController>();
        if (existing != null)
        {
            _instance = existing;
            return existing;
        }

        GameObject root = new GameObject("RelayMatchController");
        _instance = root.AddComponent<RelayMatchController>();
        return _instance;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EnsureNetworkRuntime();
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        UnregisterMessageHandlers();
        UnregisterNetworkCallbacks();
    }

    public async Task<bool> CreateMatchAsync()
    {
        if (IsBusy)
            return false;

        SetBusy(true);

        try
        {
            await EnsureServicesInitializedAsync();

            if (SessionRunning && _networkManager.IsHost && !_gameStarted && !string.IsNullOrEmpty(CurrentJoinCode))
            {
                PublishRoomCode(CurrentJoinCode);
                PublishStatus("Sala criada. Aguarde o segundo jogador.");
                return true;
            }

            ShutdownNetwork(false);
            ResetSessionState();

            PublishStatus("Criando sala...");
            PublishRoomCode("");

            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(1);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            _transport.SetRelayServerData(new RelayServerData(allocation, "dtls"));

            if (!_networkManager.StartHost())
            {
                PublishStatus("Nao foi possivel iniciar a sala.");
                return false;
            }

            RegisterMessageHandlers();

            _hostClientId = _networkManager.LocalClientId;
            CurrentJoinCode = joinCode;
            GameLaunchConfig.ConfigureCreateMatch(joinCode);
            PublishRoomCode(joinCode);
            PublishStatus("Sala criada. Aguarde o segundo jogador.");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            PublishStatus("Falha ao criar sala: " + ex.Message);
            ShutdownNetwork(false);
            return false;
        }
        finally
        {
            SetBusy(false);
        }
    }

    public async Task<bool> JoinMatchAsync(string joinCode)
    {
        if (IsBusy)
            return false;

        if (string.IsNullOrEmpty(joinCode))
        {
            PublishStatus("Informe um codigo de partida valido.");
            return false;
        }

        SetBusy(true);

        try
        {
            await EnsureServicesInitializedAsync();

            ShutdownNetwork(false);
            ResetSessionState();

            PublishStatus("Entrando na sala...");
            PublishRoomCode("");

            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            _transport.SetRelayServerData(new RelayServerData(joinAllocation, "dtls"));

            if (!_networkManager.StartClient())
            {
                PublishStatus("Nao foi possivel entrar na sala.");
                return false;
            }

            RegisterMessageHandlers();

            CurrentJoinCode = joinCode.ToUpperInvariant();
            GameLaunchConfig.ConfigureJoinMatch(CurrentJoinCode);
            PublishStatus("Conectado. Aguardando o host iniciar a partida.");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            PublishStatus("Falha ao entrar na sala: " + ex.Message);
            ShutdownNetwork(false);
            return false;
        }
        finally
        {
            SetBusy(false);
        }
    }

    public void CancelPendingSessionInMenu()
    {
        if (!SessionRunning)
        {
            CurrentJoinCode = "";
            PublishRoomCode("");
            return;
        }

        ShutdownNetwork(false);
        GameLaunchConfig.ConfigureStory(GameLaunchConfig.StoryChapter);
        PublishRoomCode("");
        PublishStatus("Escolha um modo de jogo.");
    }

    public void RequestFlip(int cardIndex)
    {
        if (!GameLaunchConfig.IsOnlineMode || !SessionRunning)
            return;

        if (_previewRunning || _gameOver)
            return;

        if (_networkManager.IsHost)
        {
            ProcessFlipRequest(_networkManager.LocalClientId, cardIndex);
            return;
        }

        using (FastBufferWriter writer = new FastBufferWriter(32, Allocator.Temp))
        {
            writer.WriteValueSafe(cardIndex);
            _networkManager.CustomMessagingManager.SendNamedMessage(FlipRequestMessage, NetworkManager.ServerClientId, writer);
        }
    }

    public void LeaveMatchAndReturnToMenu(string reason)
    {
        if (_returningToMenu)
            return;

        StartCoroutine(ReturnToMenuRoutine(reason, _networkManager != null && _networkManager.IsHost && _networkManager.IsListening));
    }

    public void RequestRematch()
    {
        if (!GameLaunchConfig.IsOnlineMode || !SessionRunning || _returningToMenu)
            return;

        if (_networkManager.IsHost)
        {
            BeginRematch();
            return;
        }

        using (FastBufferWriter writer = new FastBufferWriter(1, Allocator.Temp))
        {
            _networkManager.CustomMessagingManager.SendNamedMessage(RematchRequestMessage, NetworkManager.ServerClientId, writer);
        }

        PublishStatus("Pedido de revanche enviado ao host.");
    }

    private IEnumerator ReturnToMenuRoutine(string reason, bool notifyRemote, float delayBeforeLeaving = 0.2f)
    {
        _returningToMenu = true;

        if (notifyRemote && _networkManager != null && _networkManager.CustomMessagingManager != null)
        {
            using (FastBufferWriter writer = new FastBufferWriter(260, Allocator.Temp))
            {
                writer.WriteValueSafe(delayBeforeLeaving);
                writer.WriteValueSafe(new FixedString128Bytes(reason ?? ""));
                _networkManager.CustomMessagingManager.SendNamedMessageToAll(ReturnToMenuMessage, writer);
            }
        }

        GameLaunchConfig.ConfigureStory(GameLaunchConfig.StoryChapter);
        GameLaunchConfig.SetPendingMenuStatus(reason);
        PublishRoomCode("");

        yield return new WaitForSeconds(Mathf.Max(0f, delayBeforeLeaving));

        ShutdownNetwork(false);
        SceneManager.LoadScene(SceneIds.MainMenu);
        _returningToMenu = false;
    }

    private async Task EnsureServicesInitializedAsync()
    {
        if (!_servicesInitialized && UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

        _servicesInitialized = true;
    }

    private void EnsureNetworkRuntime()
    {
        if (_networkManager == null)
            _networkManager = FindObjectOfType<NetworkManager>();

        if (_networkManager == null)
        {
            GameObject networkRoot = new GameObject("RelayNetworkRuntime");
            DontDestroyOnLoad(networkRoot);

            _networkManager = networkRoot.AddComponent<NetworkManager>();
            _transport = networkRoot.AddComponent<UnityTransport>();
            _networkManager.NetworkConfig = new NetworkConfig();
            _networkManager.NetworkConfig.NetworkTransport = _transport;
            _networkManager.NetworkConfig.EnableSceneManagement = true;
        }
        else
        {
            DontDestroyOnLoad(_networkManager.gameObject);
            _transport = _networkManager.GetComponent<UnityTransport>();
            if (_transport == null)
                _transport = _networkManager.gameObject.AddComponent<UnityTransport>();

            if (_networkManager.NetworkConfig == null)
                _networkManager.NetworkConfig = new NetworkConfig();

            _networkManager.NetworkConfig.NetworkTransport = _transport;
            _networkManager.NetworkConfig.EnableSceneManagement = true;
        }

        RegisterNetworkCallbacks();
    }

    private void RegisterNetworkCallbacks()
    {
        if (_networkManager == null || _networkCallbacksRegistered)
            return;

        _networkManager.OnClientConnectedCallback += HandleClientConnected;
        _networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
        _networkManager.OnServerStarted += HandleServerStarted;
        _networkCallbacksRegistered = true;
    }

    private void UnregisterNetworkCallbacks()
    {
        if (_networkManager == null || !_networkCallbacksRegistered)
            return;

        _networkManager.OnClientConnectedCallback -= HandleClientConnected;
        _networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
        _networkManager.OnServerStarted -= HandleServerStarted;
        _networkCallbacksRegistered = false;
    }

    private void RegisterMessageHandlers()
    {
        if (_networkManager == null || _networkManager.CustomMessagingManager == null || _messageHandlersRegistered)
            return;

        _networkManager.CustomMessagingManager.RegisterNamedMessageHandler(StateSyncMessage, HandleStateSyncMessage);
        _networkManager.CustomMessagingManager.RegisterNamedMessageHandler(FlipRequestMessage, HandleFlipRequestMessage);
        _networkManager.CustomMessagingManager.RegisterNamedMessageHandler(ReturnToMenuMessage, HandleReturnToMenuMessage);
        _networkManager.CustomMessagingManager.RegisterNamedMessageHandler(GameplayReadyMessage, HandleGameplayReadyMessage);
        _networkManager.CustomMessagingManager.RegisterNamedMessageHandler(RematchRequestMessage, HandleRematchRequestMessage);
        _messageHandlersRegistered = true;
    }

    private void UnregisterMessageHandlers()
    {
        if (_networkManager == null || _networkManager.CustomMessagingManager == null || !_messageHandlersRegistered)
            return;

        _networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(StateSyncMessage);
        _networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(FlipRequestMessage);
        _networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(ReturnToMenuMessage);
        _networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(GameplayReadyMessage);
        _networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(RematchRequestMessage);
        _messageHandlersRegistered = false;
    }

    private void HandleServerStarted()
    {
        RegisterMessageHandlers();
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (_networkManager == null || !_networkManager.IsListening)
            return;

        if (_networkManager.IsHost)
        {
            if (clientId == _networkManager.LocalClientId)
            {
                _hostClientId = clientId;
                return;
            }

            if (_guestClientId != ulong.MaxValue && _guestClientId != clientId)
            {
                _networkManager.DisconnectClient(clientId);
                return;
            }

            _guestClientId = clientId;
            PublishStatus("Jogador 2 conectado. Iniciando a partida...");

            if (!_gameStarted)
            {
                _gameStarted = true;
                _networkManager.SceneManager.LoadScene(SceneIds.Gameplay, LoadSceneMode.Single);
            }
            else
            {
                TryBeginOrRefreshHostGameplay();
            }
        }
        else if (_networkManager.IsClient && clientId == _networkManager.LocalClientId)
        {
            PublishStatus("Conectado. Aguardando o host iniciar a partida...");
        }
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        if (_manualShutdown)
            return;

        if (_networkManager == null)
            return;

        if (_networkManager.IsHost)
        {
            if (clientId == _guestClientId)
            {
                _guestClientId = ulong.MaxValue;
                _guestGameplayReady = false;
                _turnLocked = false;
                _previewRoutineStarted = false;

                if (SceneManager.GetActiveScene().name == SceneIds.Gameplay)
                {
                    StartCoroutine(ReturnToMenuRoutine("O outro jogador saiu da partida.", false));
                }
                else
                {
                    PublishStatus("O outro jogador saiu da sala.");
                }
            }

            return;
        }

        if (_networkManager.IsClient)
        {
            GameLaunchConfig.ConfigureStory(GameLaunchConfig.StoryChapter);
            GameLaunchConfig.SetPendingMenuStatus("A conexao com a sala foi encerrada.");
            ShutdownNetwork(false);
            SceneManager.LoadScene(SceneIds.MainMenu);
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != SceneIds.Gameplay)
        {
            _gameManager = null;
            return;
        }

        if (!GameLaunchConfig.IsOnlineMode)
            return;

        _gameManager = FindObjectOfType<GameManager>();
        if (_gameManager == null)
            return;

        _gameManager.ConfigureOnlineMode();

        NotifyGameplayReady();

        if (_networkManager != null && _networkManager.IsHost)
            TryBeginOrRefreshHostGameplay();

        if (_hasSnapshot)
            ApplySnapshotToGameplay(_latestSnapshot);
    }

    private void TryBeginOrRefreshHostGameplay()
    {
        if (_networkManager == null || !_networkManager.IsHost)
            return;

        if (SceneManager.GetActiveScene().name != SceneIds.Gameplay)
            return;

        if (_gameManager == null)
            _gameManager = FindObjectOfType<GameManager>();

        if (_gameManager == null)
            return;

        _gameManager.ConfigureOnlineMode();
        EnsureBoardInitialized();

        if (!_hostGameplayReady || !HasBothPlayers() || !_guestGameplayReady)
            return;

        if (!_previewRoutineStarted)
        {
            _previewRoutineStarted = true;
            StartCoroutine(HostPreviewRoutine());
            return;
        }

        BroadcastSnapshot(BuildTurnStatusMessage());
    }

    private void EnsureBoardInitialized()
    {
        if (_boardInitialized)
            return;

        int totalCards = _gameManager != null && _gameManager.generatedCardsCount > 1 ? _gameManager.generatedCardsCount : 16;
        if (totalCards % 2 != 0)
            totalCards += 1;

        int faceCount = _gameManager != null && _gameManager.cardFace != null && _gameManager.cardFace.Length > 0 ? _gameManager.cardFace.Length : totalCards / 2;
        List<int> values = new List<int>(totalCards);
        for (int pair = 0; pair < totalCards / 2; pair++)
        {
            int value = (pair % faceCount) + 1;
            values.Add(value);
            values.Add(value);
        }

        for (int i = values.Count - 1; i > 0; i--)
        {
            int swapIndex = UnityEngine.Random.Range(0, i + 1);
            int temp = values[i];
            values[i] = values[swapIndex];
            values[swapIndex] = temp;
        }

        _boardValues = values.ToArray();
        _boardStates = new int[_boardValues.Length];
        _scores[0] = 0;
        _scores[1] = 0;
        _currentTurnSlot = 0;
        _firstSelectionIndex = -1;
        _gameOver = false;
        _turnLocked = false;
        _previewRunning = false;
        _boardInitialized = true;
    }

    private IEnumerator HostPreviewRoutine()
    {
        _previewRunning = true;

        for (int i = 0; i < _boardStates.Length; i++)
            _boardStates[i] = 1;

        BroadcastSnapshot("Memorize as cartas.");

        float delay = _gameManager != null ? Mathf.Max(1f, _gameManager.previewSeconds) : 2f;
        yield return new WaitForSeconds(delay);

        for (int i = 0; i < _boardStates.Length; i++)
            _boardStates[i] = 0;

        _previewRunning = false;
        _currentTurnSlot = 0;
        BroadcastSnapshot("Partida iniciada. Jogador 1 comeca.");
    }

    private void HandleFlipRequestMessage(ulong senderClientId, FastBufferReader reader)
    {
        if (_networkManager == null || !_networkManager.IsHost)
            return;

        int cardIndex;
        reader.ReadValueSafe(out cardIndex);
        ProcessFlipRequest(senderClientId, cardIndex);
    }

    private void ProcessFlipRequest(ulong senderClientId, int cardIndex)
    {
        if (!_boardInitialized || _previewRunning || _turnLocked || _gameOver || !HasBothPlayers())
            return;

        if (cardIndex < 0 || cardIndex >= _boardStates.Length)
            return;

        int senderSlot = GetPlayerSlot(senderClientId);
        if (senderSlot != _currentTurnSlot)
        {
            BroadcastSnapshot("Nao e o turno desse jogador.");
            return;
        }

        if (_boardStates[cardIndex] != 0)
        {
            BroadcastSnapshot("Essa carta ja foi revelada.");
            return;
        }

        _boardStates[cardIndex] = 1;

        if (_firstSelectionIndex < 0)
        {
            _firstSelectionIndex = cardIndex;
            BroadcastSnapshot("Primeira carta revelada.");
            return;
        }

        int firstCardIndex = _firstSelectionIndex;
        _firstSelectionIndex = -1;
        _turnLocked = true;

        BroadcastSnapshot("Comparando cartas...");

        if (_boardValues[firstCardIndex] == _boardValues[cardIndex])
            StartCoroutine(ResolveMatchRoutine(senderSlot, firstCardIndex, cardIndex));
        else
            StartCoroutine(ResolveMismatchRoutine(firstCardIndex, cardIndex));
    }

    private IEnumerator ResolveMatchRoutine(int playerSlot, int firstCardIndex, int secondCardIndex)
    {
        yield return new WaitForSeconds(0.6f);

        _boardStates[firstCardIndex] = 2;
        _boardStates[secondCardIndex] = 2;
        _comboStreaks[playerSlot] += 1;
        int pointsEarned = _comboStreaks[playerSlot];
        _scores[playerSlot] += pointsEarned;
        _lastScoreEventSlot = playerSlot;
        _lastPointsEarned = pointsEarned;
        _lastComboValue = _comboStreaks[playerSlot];
        _turnLocked = false;

        if (AllPairsMatched())
        {
            _gameOver = true;
            BroadcastSnapshot(BuildGameOverMessage());
            yield break;
        }

        BroadcastSnapshot("Par encontrado. Combo x" + _comboStreaks[playerSlot] + "  +" + pointsEarned + " ponto(s).");
    }

    private IEnumerator ResolveMismatchRoutine(int firstCardIndex, int secondCardIndex)
    {
        yield return new WaitForSeconds(0.8f);

        _boardStates[firstCardIndex] = 0;
        _boardStates[secondCardIndex] = 0;
        _comboStreaks[_currentTurnSlot] = 0;
        _lastScoreEventSlot = -1;
        _lastPointsEarned = 0;
        _lastComboValue = 0;
        _currentTurnSlot = _currentTurnSlot == 0 ? 1 : 0;
        _turnLocked = false;

        BroadcastSnapshot("Nao formou par. Vez do outro jogador.");
    }

    private void BroadcastSnapshot(string statusMessage)
    {
        if (_networkManager == null || !_networkManager.IsHost || _networkManager.CustomMessagingManager == null || !_boardInitialized)
            return;

        SnapshotState snapshot = BuildSnapshot(statusMessage);
        _latestSnapshot = snapshot;
        _hasSnapshot = true;

        if (_networkManager.IsHost)
            ApplySnapshotToGameplay(snapshot);

        using (FastBufferWriter writer = new FastBufferWriter(4096, Allocator.Temp))
        {
            writer.WriteValueSafe(snapshot.CardValues.Length);
            for (int i = 0; i < snapshot.CardValues.Length; i++)
                writer.WriteValueSafe(snapshot.CardValues[i]);

            for (int i = 0; i < snapshot.CardStates.Length; i++)
                writer.WriteValueSafe(snapshot.CardStates[i]);

            writer.WriteValueSafe(snapshot.ScorePlayerOne);
            writer.WriteValueSafe(snapshot.ScorePlayerTwo);
            writer.WriteValueSafe(snapshot.CurrentTurnSlot);
            writer.WriteValueSafe(snapshot.HostClientId);
            writer.WriteValueSafe(snapshot.GuestClientId);
            writer.WriteValueSafe(snapshot.PreviewRunning);
            writer.WriteValueSafe(snapshot.WaitingForOpponent);
            writer.WriteValueSafe(snapshot.GameOver);
            writer.WriteValueSafe(snapshot.WinnerSlot);
            writer.WriteValueSafe(snapshot.LastScoreEventSlot);
            writer.WriteValueSafe(snapshot.LastPointsEarned);
            writer.WriteValueSafe(snapshot.LastComboValue);
            writer.WriteValueSafe(snapshot.StatusMessage);

            _networkManager.CustomMessagingManager.SendNamedMessageToAll(StateSyncMessage, writer);
        }
    }

    private void HandleStateSyncMessage(ulong senderClientId, FastBufferReader reader)
    {
        SnapshotState snapshot = new SnapshotState();
        int count;
        reader.ReadValueSafe(out count);

        snapshot.CardValues = new int[count];
        snapshot.CardStates = new int[count];

        for (int i = 0; i < count; i++)
            reader.ReadValueSafe(out snapshot.CardValues[i]);

        for (int i = 0; i < count; i++)
            reader.ReadValueSafe(out snapshot.CardStates[i]);

        reader.ReadValueSafe(out snapshot.ScorePlayerOne);
        reader.ReadValueSafe(out snapshot.ScorePlayerTwo);
        reader.ReadValueSafe(out snapshot.CurrentTurnSlot);
        reader.ReadValueSafe(out snapshot.HostClientId);
        reader.ReadValueSafe(out snapshot.GuestClientId);
        reader.ReadValueSafe(out snapshot.PreviewRunning);
        reader.ReadValueSafe(out snapshot.WaitingForOpponent);
        reader.ReadValueSafe(out snapshot.GameOver);
        reader.ReadValueSafe(out snapshot.WinnerSlot);
        reader.ReadValueSafe(out snapshot.LastScoreEventSlot);
        reader.ReadValueSafe(out snapshot.LastPointsEarned);
        reader.ReadValueSafe(out snapshot.LastComboValue);
        reader.ReadValueSafe(out snapshot.StatusMessage);

        _latestSnapshot = snapshot;
        _hasSnapshot = true;
        ApplySnapshotToGameplay(snapshot);
    }

    private void HandleReturnToMenuMessage(ulong senderClientId, FastBufferReader reader)
    {
        float delayBeforeLeaving;
        reader.ReadValueSafe(out delayBeforeLeaving);

        FixedString128Bytes reason;
        reader.ReadValueSafe(out reason);
        string message = reason.ToString();

        if (_returningToMenu)
            return;

        StartCoroutine(ReturnToMenuRoutine(message, false, delayBeforeLeaving));
    }

    private void HandleGameplayReadyMessage(ulong senderClientId, FastBufferReader reader)
    {
        if (_networkManager == null || !_networkManager.IsHost)
            return;

        if (senderClientId == _hostClientId)
            _hostGameplayReady = true;
        else if (senderClientId == _guestClientId)
            _guestGameplayReady = true;

        TryBeginOrRefreshHostGameplay();
    }

    private void HandleRematchRequestMessage(ulong senderClientId, FastBufferReader reader)
    {
        if (_networkManager == null || !_networkManager.IsHost || !_gameOver)
            return;

        BeginRematch();
    }

    private void ApplySnapshotToGameplay(SnapshotState snapshot)
    {
        if (SceneManager.GetActiveScene().name != SceneIds.Gameplay)
            return;

        if (_gameManager == null)
            _gameManager = FindObjectOfType<GameManager>();

        if (_gameManager == null)
            return;

        _gameManager.ConfigureOnlineMode();

        int localSlot = GetLocalPlayerSlot(snapshot);
        _gameManager.ApplyOnlineSnapshot(
            snapshot.CardValues,
            snapshot.CardStates,
            snapshot.ScorePlayerOne,
            snapshot.ScorePlayerTwo,
            snapshot.CurrentTurnSlot,
            localSlot,
            snapshot.PreviewRunning,
            snapshot.WaitingForOpponent,
            snapshot.GameOver,
            snapshot.WinnerSlot,
            snapshot.LastScoreEventSlot,
            snapshot.LastPointsEarned,
            snapshot.LastComboValue,
            snapshot.StatusMessage.ToString());
    }

    private SnapshotState BuildSnapshot(string statusMessage)
    {
        SnapshotState snapshot = new SnapshotState();
        snapshot.CardValues = (int[])_boardValues.Clone();
        snapshot.CardStates = (int[])_boardStates.Clone();
        snapshot.ScorePlayerOne = _scores[0];
        snapshot.ScorePlayerTwo = _scores[1];
        snapshot.CurrentTurnSlot = _currentTurnSlot;
        snapshot.HostClientId = _hostClientId;
        snapshot.GuestClientId = _guestClientId;
        snapshot.PreviewRunning = _previewRunning || _turnLocked;
        snapshot.WaitingForOpponent = !HasBothPlayers();
        snapshot.GameOver = _gameOver;
        snapshot.WinnerSlot = GetWinnerSlot();
        snapshot.LastScoreEventSlot = _lastScoreEventSlot;
        snapshot.LastPointsEarned = _lastPointsEarned;
        snapshot.LastComboValue = _lastComboValue;
        snapshot.StatusMessage = new FixedString128Bytes(statusMessage ?? "");
        return snapshot;
    }

    private int GetWinnerSlot()
    {
        if (!_gameOver)
            return -2;

        if (_scores[0] == _scores[1])
            return -1;

        return _scores[0] > _scores[1] ? 0 : 1;
    }

    private int GetPlayerSlot(ulong clientId)
    {
        if (clientId == _hostClientId)
            return 0;

        if (clientId == _guestClientId)
            return 1;

        return -1;
    }

    private int GetLocalPlayerSlot(SnapshotState snapshot)
    {
        if (_networkManager == null)
            return -1;

        ulong localClientId = _networkManager.LocalClientId;
        if (localClientId == snapshot.HostClientId)
            return 0;

        if (localClientId == snapshot.GuestClientId)
            return 1;

        return -1;
    }

    private bool HasBothPlayers()
    {
        return _hostClientId != ulong.MaxValue && _guestClientId != ulong.MaxValue;
    }

    private bool AllPairsMatched()
    {
        for (int i = 0; i < _boardStates.Length; i++)
        {
            if (_boardStates[i] != 2)
                return false;
        }

        return true;
    }

    private string BuildTurnStatusMessage()
    {
        return _currentTurnSlot == 0 ? "Vez do Jogador 1." : "Vez do Jogador 2.";
    }

    private string BuildGameOverMessage()
    {
        if (_scores[0] == _scores[1])
            return "Empate. Missao concluida.";

        return _scores[0] > _scores[1] ? "Jogador 1 venceu a partida." : "Jogador 2 venceu a partida.";
    }

    private void BeginRematch()
    {
        if (_networkManager == null || !_networkManager.IsHost || !_networkManager.IsListening)
            return;

        ResetMatchStateKeepPlayers();
        PublishStatus("Reiniciando a partida...");
        _networkManager.SceneManager.LoadScene(SceneIds.Gameplay, LoadSceneMode.Single);
    }

    private void ResetMatchStateKeepPlayers()
    {
        _boardInitialized = false;
        _previewRoutineStarted = false;
        _previewRunning = false;
        _turnLocked = false;
        _gameOver = false;
        _firstSelectionIndex = -1;
        _scores[0] = 0;
        _scores[1] = 0;
        _comboStreaks[0] = 0;
        _comboStreaks[1] = 0;
        _lastScoreEventSlot = -1;
        _lastPointsEarned = 0;
        _lastComboValue = 0;
        _currentTurnSlot = 0;
        _boardValues = Array.Empty<int>();
        _boardStates = Array.Empty<int>();
        _hostGameplayReady = false;
        _guestGameplayReady = false;
        _hasSnapshot = false;
        _latestSnapshot = new SnapshotState();
    }

    private void ResetSessionState()
    {
        _gameStarted = false;
        _boardInitialized = false;
        _previewRoutineStarted = false;
        _previewRunning = false;
        _turnLocked = false;
        _gameOver = false;
        _firstSelectionIndex = -1;
        _hostClientId = ulong.MaxValue;
        _guestClientId = ulong.MaxValue;
        _scores[0] = 0;
        _scores[1] = 0;
        _comboStreaks[0] = 0;
        _comboStreaks[1] = 0;
        _lastScoreEventSlot = -1;
        _lastPointsEarned = 0;
        _lastComboValue = 0;
        _currentTurnSlot = 0;
        _boardValues = Array.Empty<int>();
        _boardStates = Array.Empty<int>();
        _hostGameplayReady = false;
        _guestGameplayReady = false;
        _hasSnapshot = false;
        _latestSnapshot = new SnapshotState();
    }

    private void ShutdownNetwork(bool keepStatusMessage)
    {
        UnregisterMessageHandlers();

        if (_networkManager != null && _networkManager.IsListening)
        {
            _manualShutdown = true;
            _networkManager.Shutdown();
            _manualShutdown = false;
        }

        ResetSessionState();

        if (!keepStatusMessage)
            PublishStatus("");

        CurrentJoinCode = "";
        PublishRoomCode("");
    }

    private void PublishStatus(string message)
    {
        CurrentStatusMessage = message ?? "";
        StatusChanged?.Invoke(CurrentStatusMessage);
    }

    private void PublishRoomCode(string roomCode)
    {
        CurrentJoinCode = roomCode ?? "";
        RoomCodeChanged?.Invoke(CurrentJoinCode);
    }

    private void SetBusy(bool busy)
    {
        IsBusy = busy;
        BusyStateChanged?.Invoke(IsBusy);
    }

    private void NotifyGameplayReady()
    {
        if (_networkManager == null || !_networkManager.IsListening)
            return;

        if (_networkManager.IsHost)
        {
            _hostGameplayReady = true;
            return;
        }

        using (FastBufferWriter writer = new FastBufferWriter(4, Allocator.Temp))
        {
            _networkManager.CustomMessagingManager.SendNamedMessage(GameplayReadyMessage, NetworkManager.ServerClientId, writer);
        }
    }

    private struct SnapshotState
    {
        public int[] CardValues;
        public int[] CardStates;
        public int ScorePlayerOne;
        public int ScorePlayerTwo;
        public int CurrentTurnSlot;
        public ulong HostClientId;
        public ulong GuestClientId;
        public bool PreviewRunning;
        public bool WaitingForOpponent;
        public bool GameOver;
        public int WinnerSlot;
        public int LastScoreEventSlot;
        public int LastPointsEarned;
        public int LastComboValue;
        public FixedString128Bytes StatusMessage;
    }
}

```
## Conclusao para apresentar ao grupo

O protocolo criado funciona como um lobby TCP separado da partida. Ele organiza os jogadores antes do jogo com mensagens JSON simples. Quando os dois jogadores estao prontos, o TCP apenas entrega o codigo do Unity Relay. A sincronizacao real da partida fica no `RelayMatchController`, usando Netcode for GameObjects. Essa separacao deixa o projeto mais facil de entender, porque o lobby e a partida ficam com responsabilidades diferentes.




