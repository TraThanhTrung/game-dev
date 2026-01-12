# Giải Thích: HTTP Input vs SignalR

## Vấn Đề

Khi đã connect SignalR, vẫn thấy HTTP POST `/sessions/input` requests trong logs.

## Nguyên Nhân

### Unity Client Chưa Implement SignalR Thật

Unity client hiện tại **KHÔNG** sử dụng SignalR client thật, mà chỉ **simulate SignalR bằng HTTP polling**.

**Code trong `NetClient.cs`:**

```csharp
// Line 400-423
// Since Unity doesn't have native SignalR client, we simulate it with WebSocket polling
// In production, use SignalR Unity package or implement WebSocket client
// For now, we use a faster polling approach as SignalR simulation
```

**Method `SendInputViaSignalR()` (line 470-480):**

```csharp
public void SendInputViaSignalR(SignalRInputPayload input)
{
    if (!m_IsSignalRConnected)
        return;

    input.playerId = PlayerId.ToString();
    input.sessionId = SessionId;

    // Send via REST (simulating SignalR)  <-- Vẫn dùng HTTP!
    StartCoroutine(SendInputCoroutine(input));
}
```

**`SendInputCoroutine()` (line 611-635):**

```csharp
private IEnumerator SendInputCoroutine(SignalRInputPayload input)
{
    // Convert to regular InputPayload for existing endpoint
    var legacyInput = new InputPayload { ... };

    // Gọi SendInput() - mà SendInput() lại gửi HTTP POST!
    yield return SendInput(legacyInput, ...);
}
```

### Server Có SignalR Hub Method

Server **CÓ** SignalR hub method `SendInput()` trong `GameHub.cs`:

```csharp
public async Task SendInput(InputPayload input)
{
    _worldService.QueueInput(input);
    await Task.CompletedTask;
}
```

Nhưng Unity client **KHÔNG GỌI** method này, mà vẫn gửi HTTP POST.

## Kết Quả

- ✅ Unity client "connect SignalR" (thực tế là start HTTP polling với interval ngắn hơn)
- ✅ Unity client gọi `SendInputViaSignalR()` (nhưng vẫn gửi HTTP POST)
- ✅ Server nhận HTTP POST `/sessions/input` (đây là expected behavior)

## Giải Pháp

### Option 1: Chấp Nhận Hiện Tại (Khuyến Nghị)

HTTP input endpoint vẫn hoạt động tốt. Đây là fallback mechanism hợp lý.

**Ưu điểm:**

- Đơn giản, dễ debug
- Không cần SignalR Unity package
- Hoạt động ổn định

**Nhược điểm:**

- Overhead HTTP headers
- Latency cao hơn SignalR một chút

### Option 2: Implement SignalR Unity Client Thật

Sử dụng **Microsoft.AspNetCore.SignalR.Client** package cho Unity:

1. Install package:

   ```
   https://github.com/dotnet/aspnetcore/tree/main/src/SignalR/clients/csharp/Http.Connections.Client
   ```

2. Sửa `NetClient.cs`:

   ```csharp
   using Microsoft.AspNetCore.SignalR.Client;

   private HubConnection? m_HubConnection;

   public IEnumerator ConnectSignalRAsync(...)
   {
       m_HubConnection = new HubConnectionBuilder()
           .WithUrl($"{m_BaseUrl}/gamehub")
           .Build();

       m_HubConnection.On<GameStateSnapshot>("ReceiveGameState", OnGameStateReceived);

       yield return m_HubConnection.StartAsync();
   }

   public void SendInputViaSignalR(SignalRInputPayload input)
   {
       if (m_HubConnection == null) return;

       // Gọi SignalR hub method thật
       m_HubConnection.SendAsync("SendInput", input);
   }
   ```

**Ưu điểm:**

- Real-time, low latency
- WebSocket connection
- Server push (không cần polling)

**Nhược điểm:**

- Cần thêm dependency
- Phức tạp hơn
- Cần handle reconnection logic

### Option 3: Disable HTTP Input Khi SignalR Connected

Có thể disable HTTP input endpoint khi detect SignalR connection, nhưng:

- Unity client chưa dùng SignalR thật → sẽ break
- Cần check connection state (phức tạp)

## Kết Luận

**Hiện tại: HTTP input requests là EXPECTED BEHAVIOR.**

Unity client chưa implement SignalR client thật, chỉ simulate bằng HTTP polling. Điều này hoạt động tốt và không có vấn đề gì.

Nếu muốn dùng SignalR thật, cần implement SignalR Unity client (Option 2).

---

**Lưu ý:** Server có sẵn SignalR hub method `SendInput()`, nhưng Unity client chưa gọi nó. HTTP endpoint vẫn cần thiết cho Unity client hiện tại.
