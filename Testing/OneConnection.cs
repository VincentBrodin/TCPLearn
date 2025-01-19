using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using TCPLearn;

namespace Testing;

[TestClass]
public class OneConnection {
	[TestMethod]
	public void StartServer() {
		const int port = 4202;
		Assert.IsTrue(IsFree(port));

		Server server = new(IPAddress.Loopback, port);
		server.Start();
		Assert.IsTrue(server.IsRunning);
		server.Stop();
		Assert.IsFalse(server.IsRunning);
	}

	[TestMethod]
	public async Task ConnectOneClient() {
		const int port = 4201;
		Assert.IsTrue(IsFree(port));

		Server server = new(IPAddress.Loopback, port);
		server.Start();
		Assert.IsTrue(server.IsRunning);

		Client client = new();

		client.Connect(IPAddress.Loopback, port);
		Assert.IsTrue(client.IsRunning);


		Task timeout = Task.Delay(5000);
		Task waitForServer = Task.Run(() => {
			while (server.GetClients().Length == 0) ;
		});

		Task completedTask = await Task.WhenAny(waitForServer, timeout);
		Console.WriteLine($"{server.GetClients().Length} client(s) connected.");

		if (completedTask == timeout) {
			Assert.Fail("Test timed out.");
		}

		Assert.IsTrue(server.GetClients().Length == 1);

		client.Disconnect();
		Assert.IsFalse(client.IsRunning);
		Assert.IsTrue(server.IsRunning);

		server.Stop();
		Assert.IsFalse(server.IsRunning);
	}


	[TestMethod]
	public async Task ConnectMany() {
		const int port = 4200;
		Assert.IsTrue(IsFree(port));

		Server server = new(IPAddress.Loopback, port);
		server.Start();
		Assert.IsTrue(server.IsRunning);

		List<Client> clients = [];

		for (int i = 0; i < 100; i++) {
			Client client = new();
			client.Connect(IPAddress.Loopback, port);
			clients.Add(client);
			Assert.IsTrue(client.IsRunning);
		}

		Task timeout = Task.Delay(5000);
		Task waitForServer = Task.Run(() => {
			while (server.GetClients().Length != clients.Count) ;
		});

		Task completedTask = await Task.WhenAny(waitForServer, timeout);

		Console.WriteLine($"{server.GetClients().Length} client(s) connected to server out of {clients.Count} client that tried to connect.");

		if (completedTask == timeout) {
			Assert.Fail("Test timed out.");
		}
		Assert.IsTrue(server.GetClients().Length == clients.Count);

		server.Stop();
		Assert.IsFalse(server.IsRunning);
	}

	[TestMethod]
	public async Task ManyMessages() {
		const int port = 4203;
		const uint handlerId = 1;
		List<int> clientMessages = [];
		List<Client> clients = [];
		Assert.IsTrue(IsFree(port));

		Server server = new(IPAddress.Loopback, port);
		server.AddHandler(handlerId, (int clientId, byte[] _, CancellationToken _) => {
			lock (clientMessages) {
				clientMessages.Add(clientId);
				Console.WriteLine($"Got {clientId} message.");
			}
			return Task.CompletedTask;
		});

		server.Start();

		Assert.IsTrue(server.IsRunning);


		for (int i = 0; i < 100; i++) {
			Client client = new();
			client.Connect(IPAddress.Loopback, port);
			clients.Add(client);
			Assert.IsTrue(client.IsRunning);
		}

		// Wait for connection
		Task timeout = Task.Delay(5000);
		Task waitForServer = Task.Run(() => {
			while (server.GetClients().Length != clients.Count) ;
		});

		Task completedTask = await Task.WhenAny(waitForServer, timeout);

		Console.WriteLine($"{server.GetClients().Length} client(s) connected to server out of {clients.Count} client that tried to connect.");
		if (completedTask == timeout) {
			Assert.Fail("Test timed out.");
		}
		Assert.IsTrue(server.GetClients().Length == clients.Count);

		// When all clients are connected we send messages
		List<Task> sends = [];
		foreach (Client client in clients) {
			sends.Add(Task.Run(async () => {
				byte[] data = Encoding.UTF8.GetBytes("Hello, world");
				await client.SendMessage(handlerId, data);
			}));
		}

		Task.WaitAll(sends);

		timeout = Task.Delay(10000);
		waitForServer = Task.Run(() => {
			while (clientMessages.Count != clients.Count) ;
		});
		completedTask = await Task.WhenAny(waitForServer, timeout);
		Console.WriteLine($"Got {clientMessages.Count} client(s) messages to server out of {clients.Count} client that tried to send messages.");
		if (completedTask == timeout) {
			Assert.Fail("Test timed out.");
		}
		Assert.IsTrue(clientMessages.Count == clients.Count);

		server.Stop();
		Assert.IsFalse(server.IsRunning);
	}




	private bool IsFree(int port) {
		IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();
		IPEndPoint[] listeners = properties.GetActiveTcpListeners();
		int[] openPorts = listeners.Select(item => item.Port).ToArray<int>();
		return openPorts.All(openPort => openPort != port);
	}
}
