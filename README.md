# TCP Learn
**TCP Learn** is a project aimed at eploring and understanding TCP socket programming and multithreaded code in C#.
It provides a wrapper for the `TCPServer` and `TCPClient` classes in the `System.Net` namespace,
simplifying the implementation of non-blocking, multithreade client-server communication.

This solution also includes demo projects to showcase the wrappers usage and a testing project.

### Purpose
The main objective of this project is to enhance my understanding of:
- TCP sockets and networking fundamentals in C#.
- Implementing non-blocking, multithreaded communication.
- Designing modular and reusable code.
- Writing robust and testable code for networked applications.

### Solution Structure
The solution is organized into four distinct projects:

1. **TcpLearn**: Contains the core wrapper classes for `TCPServer` and `TCPClient`.
2. **Client**: A demo application that demonstrates how to use the `TCPClient` wrapper to connect to a server, send messages, and handle responses.
3. **Server**: A demo application that illustrates the use of the `TCPServer` wrapper to manage multiple clients, register handlers, and process incoming messages.
4. **Testing**: A project that tests the functionality of the `TCPServer` and `TCPClient` together, simulating  real-world (ish) client-server scenarios.

