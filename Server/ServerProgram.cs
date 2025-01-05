using System.Net;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.Json;

namespace TCPLearn;

public enum Handlers {
	None,
	Message,
	SetUsername,
}

public struct Message {
	public string Username { get; set; }
	public string Content { get; set; }

	public Message(string username, string content) {
		Username = username;
		Content = content;
	}
}

public static class ServerProgram {

	public static void Main(string[] args) {
		Server server = new(IPAddress.Loopback, 4200);

#if DEBUG
		//server.Delay = 2000;
#endif

		Dictionary<int, string> usernames = [];

		server.AddHandler((uint)Handlers.SetUsername, (int clientId, byte[] dataBuffer, CancellationToken _) => {
			string username = Encoding.UTF8.GetString(dataBuffer);
			Console.WriteLine($"{clientId} set username to {username}");
			usernames[clientId] = username;
			return Task.CompletedTask;
		});

		server.AddHandler((uint)Handlers.Message, async (int clientId, byte[] dataBuffer, CancellationToken cancellationToken) => {
			// Grab the content of the message and create a new message object.
			string content = Encoding.UTF8.GetString(dataBuffer);
			Message message = new(usernames[clientId], content);
			// Convert the message object into a json string and then into a byte array.
			byte[] messageBuffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));

			// Get the list of all connected clients without the user that sent it.
			int[] clientIds = server.GetClients().Where(n => n != clientId).ToArray();
			await server.SendMessage(clientIds, (uint)Handlers.Message, messageBuffer, cancellationToken);
		});

		server.Start();
		Console.ReadKey();
		Console.WriteLine();
		server.Stop();
	}
}