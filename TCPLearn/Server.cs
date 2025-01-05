using System.Net;
using System.Net.Sockets;

namespace TCPLearn;



public class Server {
	public delegate Task MessageHandler(int clientId, byte[] data, CancellationToken cancellationToken);
#if DEBUG
	public int Delay { get; set; }
#endif

	private readonly TcpListener listener;
	private bool isRunning;

	private readonly Dictionary<int, TcpClient> connectedClients = [];
	private int runningClientId;

	private readonly List<Task> activeTasks = [];
	private CancellationTokenSource cancellationTokenSource = new();

	private readonly Dictionary<uint, MessageHandler> messageHandlers = [];

	public Server(IPAddress iPAddress, int port) {
		IPEndPoint endPoint = new(iPAddress, port);
		listener = new(endPoint);
	}

	/// <summary>
	/// Starts the server and begins listening for incoming client connections.
	/// </summary>
	public void Start() {
		listener.Start();

		cancellationTokenSource = new CancellationTokenSource();
		activeTasks.Add(Task.Run(() => TaskAcceptListener(cancellationTokenSource.Token)));

		isRunning = true;

		Console.WriteLine("Server started.");
	}

	/// <summary>
	/// Stops the server and disconnects all clients.
	/// </summary>
	public void Stop() {
		if (!isRunning) {
			return;
		}
		isRunning = false;

		cancellationTokenSource.Cancel();

		listener.Stop();


		Console.WriteLine("Waiting for all tasks to complete...");
		try {
			activeTasks.RemoveAll(task => task.IsCompleted);
			Task.WhenAll(activeTasks).Wait();
		}
		catch (Exception exception) {
			Console.WriteLine($"Exception while waiting for tasks: {exception.Message}");
		}

		Console.WriteLine("Server stopped.");
	}

	/// <summary>
	/// Registers a handler for processing messages from clients with a specific ID.
	/// </summary>
	/// <param name="handlerId">The unique identifier for the message type.</param>
	/// <param name="handler">The function to handle the message.</param>
	public void AddHandler(uint handlerId, MessageHandler handler) {
		messageHandlers.Add(handlerId, handler);
	}

	private async Task TaskAcceptListener(CancellationToken cancellationToken) {
		try {
			while (!cancellationToken.IsCancellationRequested) {
				Console.WriteLine("Waiting for client.");
				TcpClient client = await listener.AcceptTcpClientAsync(cancellationToken);
				int clientId = runningClientId;
				connectedClients.Add(clientId, client);
				activeTasks.Add(Task.Run(() => TaskListenToClient(clientId, cancellationToken)));
				runningClientId++;
				Console.WriteLine("New client connected.");

#if DEBUG
				await Task.Delay(Delay, cancellationToken);
#endif

			}
		}
		catch (OperationCanceledException) {
			Console.WriteLine("Accept listener canceled.");
		}
		catch (Exception exception) {
			Console.WriteLine($"Exception in accept listener: {exception.Message}");
		}
	}

	private async Task TaskListenToClient(int clientId, CancellationToken cancellationToken) {
		try {
			while (!cancellationToken.IsCancellationRequested) {
				if (connectedClients.TryGetValue(clientId, out TcpClient? client) && client != null) {
#if DEBUG
					//In delay
					await Task.Delay(Delay, cancellationToken);
#endif
					// Handler id
					byte[] handlerIdBuffer = new byte[4];
					await client.GetStream().ReadExactlyAsync(handlerIdBuffer, cancellationToken);
					uint handlerId = BitConverter.ToUInt32(handlerIdBuffer);

#if DEBUG
					//Out delay
					await Task.Delay(Delay, cancellationToken);
#endif

					//Not used anymore but could be fun to keep.
					if (handlerId == 0) {
						Console.WriteLine("Pong");
						handlerIdBuffer = BitConverter.GetBytes((uint)0);
						await client.GetStream().WriteAsync(handlerIdBuffer, cancellationToken);
					}
					// Handler
					else if (messageHandlers.TryGetValue(handlerId, out MessageHandler? handler) && handler != null) {
						// Size of the next message
						byte[] sizeBuffer = new byte[4];
						await client.GetStream().ReadExactlyAsync(sizeBuffer, cancellationToken);
						uint size = BitConverter.ToUInt32(sizeBuffer);

						// Message content
						byte[] dataBuffer = new byte[size];
						await client.GetStream().ReadExactlyAsync(dataBuffer, cancellationToken);
						await handler.Invoke(clientId, dataBuffer, cancellationToken);
					}
					// No Handler
					else {
						Console.WriteLine($"No handler for message {handlerId}");
					}
				}
			}
		}
		catch (OperationCanceledException) {
			Console.WriteLine($"Listening to {clientId} canceled.");
		}
		catch (Exception exception) {
			Console.WriteLine($"Exception from {clientId}: {exception.Message}");
		}
		finally {
			if (connectedClients.TryGetValue(clientId, out TcpClient? client) && client != null) {
				client.Dispose();
				connectedClients.Remove(clientId);
			}

			Console.WriteLine($"Disconnected {clientId}");
		}
	}

	/// <summary>
	/// Sends a message to a specific client.
	/// </summary>
	/// <param name="clientId">The ID of the client.</param>
	/// <param name="handlerId">The unique identifier for the message type.</param>
	/// <param name="dataBuffer">The message data.</param>
	/// <param name="cancellationToken">(Optional) A token to monitor for cancellation requests.</param>
	/// <returns></returns>
	public async Task SendMessage(int clientId, uint handlerId, byte[] dataBuffer, CancellationToken cancellationToken = new()) {
		try {
			if (connectedClients.TryGetValue(clientId, out TcpClient? client) && client != null) {
				NetworkStream ns = client.GetStream();
				byte[] handlerIdBuffer = BitConverter.GetBytes(handlerId);
				await ns.WriteAsync(handlerIdBuffer, cancellationToken);

				byte[] sizeIdBuffer = BitConverter.GetBytes((uint)dataBuffer.Length);
				await ns.WriteAsync(sizeIdBuffer, cancellationToken);

				await ns.WriteAsync(dataBuffer, cancellationToken);
				Console.WriteLine($"Message sent to {clientId}");
			}
		}
		catch (OperationCanceledException) {
			Console.WriteLine("Send messaged canceled");
		}
		catch (Exception exception) {
			Console.WriteLine($"Exception trying to send message: {exception.Message}");
		}
	}

	/// <summary>
	/// Sends a message to multiple clients.
	/// </summary>
	/// <param name="clientsId">A collection of client IDs.</param>
	/// <param name="handlerId">The unique identifier for the message type.</param>
	/// <param name="dataBuffer">The message data.</param>
	/// <param name="cancellationToken">(Optional) A token to monitor for cancellation requests.</param>
	/// <returns></returns>
	public async Task SendMessage(IEnumerable<int> clientsId, uint handlerId, byte[] dataBuffer, CancellationToken cancellationToken = new()) {
		try {
			foreach (int clientId in clientsId) {
				if (connectedClients.TryGetValue(clientId, out TcpClient? client) && client != null) {
					NetworkStream ns = client.GetStream();
					byte[] handlerIdBuffer = BitConverter.GetBytes(handlerId);
					await ns.WriteAsync(handlerIdBuffer, cancellationToken);

					byte[] sizeIdBuffer = BitConverter.GetBytes((uint)dataBuffer.Length);
					await ns.WriteAsync(sizeIdBuffer, cancellationToken);

					await ns.WriteAsync(dataBuffer, cancellationToken);

					Console.WriteLine($"Message sent to {clientId}");
				}
			}
		}
		catch (OperationCanceledException) {
			Console.WriteLine("Send messaged canceled");
		}
		catch (Exception exception) {
			Console.WriteLine($"Exception trying to send message: {exception.Message}");
		}
	}

	/// <summary>
	/// Retrieves the list of connected client IDs.
	/// </summary>
	/// <returns>An array of integers representing the IDs of connected clients.</returns>
	public int[] GetClients() {
		return [.. connectedClients.Keys];
	}
}
