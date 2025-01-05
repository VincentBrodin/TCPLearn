using System.Net;
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

public static class ClientProgram {
	public static async Task Main(string[] args) {
		Client client = new();

		client.AddHandler((uint)Handlers.Message, (byte[] buffer, CancellationToken _) => {
			Message message = JsonSerializer.Deserialize<Message>(Encoding.UTF8.GetString(buffer));
			Console.WriteLine($"{message.Username} said: {message.Content}");
			return Task.CompletedTask;
		});

		client.Connect(IPAddress.Loopback, 4200);

		string username = GetUsernameInput();
		byte[] usernameBuffer = Encoding.UTF8.GetBytes(username);
		await client.SendMessage((uint)Handlers.SetUsername, usernameBuffer);

		while (true) {
			string? input = Console.ReadLine();
			if (string.IsNullOrEmpty(input)) {
				continue;
			}

			if (input.Equals("exit", StringComparison.CurrentCultureIgnoreCase)) {
				break;
			}

			if (input.Equals("username", StringComparison.CurrentCultureIgnoreCase)) {
				username = GetUsernameInput();
				usernameBuffer = Encoding.UTF8.GetBytes(username);
				await client.SendMessage((uint)Handlers.SetUsername, usernameBuffer);
			}

			byte[] dataBuffer = Encoding.UTF8.GetBytes(input);
			await client.SendMessage((uint)Handlers.Message, dataBuffer);
		}
		client.Disconnect();
	}

	private static string GetUsernameInput() {
		string? username = null;
		while (string.IsNullOrEmpty(username)) {
			Console.Write("Set username: ");
			username = Console.ReadLine();
		}
		return username;
	}
}
