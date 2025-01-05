using System.Net;
using System.Net.Sockets;

namespace TCPLearn;

public class Client {
	public delegate void ConnectionChanged();
	public delegate Task MessageHandler(byte[] data, CancellationToken cancellationToken);

	public event ConnectionChanged? OnDisconnect;

	private readonly TcpClient tcpClient;
	private bool isRunning;

	private readonly List<Task> activeTasks = [];
	private readonly CancellationTokenSource connectedTokenSource = new();

	private readonly Dictionary<uint, MessageHandler> messageHandlers = [];

	public Client() {
		tcpClient = new TcpClient();

		OnDisconnect += Disconnect;
	}

	/// <summary>
	/// Establishes a connection to a server at the specified IP address and port.
	/// </summary>
	/// <param name="iPAddress">The IP address of the server.</param>
	/// <param name="port">The port to connect to.</param>
	public void Connect(IPAddress iPAddress, int port) {
		try {
			tcpClient.Connect(iPAddress, port);
		}
		catch (Exception exception) {
			Console.WriteLine($"Could not connect: {exception.Message}");
			tcpClient.Close();
			return;
		}
		isRunning = true;

		activeTasks.Add(Task.Run(() => TaskListenForMessages(connectedTokenSource.Token), connectedTokenSource.Token));

		Console.WriteLine("Connected.");
	}

	/// <summary>
	/// Closes the connection to the server and stops all associated tasks.
	/// </summary>
	public void Disconnect() {
		if (!isRunning) {
			return;
		}
		isRunning = false;

		tcpClient.Close();
		connectedTokenSource.Cancel();


		Console.WriteLine("Waiting for all tasks to complete...");
		try {
			activeTasks.RemoveAll(task => task.IsCompleted);
			Task.WhenAll(activeTasks).Wait();
		}
		catch (Exception exception) {
			Console.WriteLine($"Exception while waiting for tasks: {exception.Message}");
		}

		Console.WriteLine("Disconnected.");
	}

	/// <summary>
	/// Registers a handler for processing incoming messages with a specific ID.
	/// </summary>
	/// <param name="handlerId">The unique identifier for the message type.</param>
	/// <param name="handler">The function to handle the message.</param>
	public void AddHandler(uint handlerId, MessageHandler handler) {
		messageHandlers.Add(handlerId, handler);
	}

	private async Task TaskListenForMessages(CancellationToken cancellationToken) {
		try {
			while (!cancellationToken.IsCancellationRequested) {
				byte[] handlerIdBuffer = new byte[4];
				await tcpClient.GetStream().ReadExactlyAsync(handlerIdBuffer, cancellationToken);
				uint handlerId = BitConverter.ToUInt32(handlerIdBuffer);

				//Not used anymore :)
				if (handlerId == 0) {
					Console.WriteLine("Ping");
					byte[] buffer = BitConverter.GetBytes((uint)0);
					await tcpClient.GetStream().WriteAsync(buffer, cancellationToken);
				}
				else if (messageHandlers.TryGetValue(handlerId, out MessageHandler? handler) && handler != null) {
					byte[] sizeBuffer = new byte[4];
					await tcpClient.GetStream().ReadExactlyAsync(sizeBuffer, cancellationToken);
					uint size = BitConverter.ToUInt32(sizeBuffer);

					byte[] dataBuffer = new byte[size];
					await tcpClient.GetStream().ReadExactlyAsync(dataBuffer, cancellationToken);
					await handler.Invoke(dataBuffer, cancellationToken);
				}
				else {
					Console.WriteLine($"No handler for message {handlerId}");
				}
			}
		}
		catch (OperationCanceledException) {
			Console.WriteLine("Listen to messages canceld.");
		}
		catch (Exception exception) {
			Console.WriteLine($"Exception in listen for messages: {exception.Message}");
			_ = Task.Run(() => OnDisconnect?.Invoke());
		}
	}

	/// <summary>
	/// Sends a message to the server.
	/// </summary>
	/// <param name="handlerId">The unique identifier for the message type.</param>
	/// <param name="dataBuffer">The message data.</param>
	/// <param name="cancellationToken">(Optional) A token to monitor for cancellation requests.</param>
	public async Task SendMessage(uint handlerId, byte[] dataBuffer, CancellationToken cancellationToken = new()) {
		try {
			NetworkStream ns = tcpClient.GetStream();
			byte[] handlerIdBuffer = BitConverter.GetBytes(handlerId);
			await ns.WriteAsync(handlerIdBuffer, cancellationToken);

			byte[] sizeIdBuffer = BitConverter.GetBytes((uint)dataBuffer.Length);
			await ns.WriteAsync(sizeIdBuffer, cancellationToken);

			await ns.WriteAsync(dataBuffer, cancellationToken);
		}
		catch (OperationCanceledException) {
			Console.WriteLine("Send messaged canceled");
		}
		catch (Exception exception) {
			Console.WriteLine($"Exception trying to send message: {exception.Message}");
		}
	}
}
