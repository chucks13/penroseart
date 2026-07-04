using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;

#if ENABLE_TELNET
public interface ICommand
{
    string Name { get; }
    string Description { get; }
    string Execute(string[] args, CommandRegistry registry);
}

/// <summary>
/// Registry for optional telnet commands. Compiled only when ENABLE_TELNET is defined.
/// </summary>
public class CommandRegistry
{
    private readonly Dictionary<string, ICommand> _commands = new Dictionary<string, ICommand>();

    public CommandRegistry(Controller controller)
    {
        Controller = controller ?? throw new ArgumentNullException(nameof(controller));
    }

    public Controller Controller { get; }

    /// <summary>
    /// Adds or replaces a command by name.
    /// </summary>
    public void Register(ICommand command)
    {
        _commands[command.Name] = command;
    }

    /// <summary>
    /// Looks up a command by name, returning null when not registered.
    /// </summary>
    public ICommand GetCommand(string name)
    {
        ICommand cmd;
        if (_commands.TryGetValue(name, out cmd))
            return cmd;
        return null;
    }

    /// <summary>
    /// Returns all registered commands for help/listing output.
    /// </summary>
    public IEnumerable<ICommand> GetAllCommands()
    {
        return _commands.Values;
    }
}

/// <summary>
/// Telnet command that lists available commands.
/// </summary>
public class HelpCommand : ICommand
{
    public string Name { get { return "help"; } }
    public string Description { get { return "Displays help for a specific command"; } }

    public string Execute(string[] args, CommandRegistry registry)
    {
        if (args.Length == 0)
        {
            var lines = new List<string> { "Available commands:" };
            foreach (var cmd in registry.GetAllCommands())
            {
                lines.Add("  " + cmd.Name + " - " + cmd.Description);
            }
            return string.Join("\r\n", lines);
        }
        else
        {
            var cmd = registry.GetCommand(args[0]);
            if (cmd != null)
                return cmd.Name + ": " + cmd.Description;
            else
                return "Command '" + args[0] + "' not found.";
        }
    }
}
/// <summary>
/// Telnet command that echoes arguments back to the client.
/// </summary>
public class EchoCommand : ICommand
{
    public string Name { get { return "echo"; } }
    public string Description { get { return "Echoes the input text"; } }

    public string Execute(string[] args, CommandRegistry registry)
    {
        return string.Join(" ", args);
    }
}

/// <summary>
/// Telnet command that lists runtime catalogs such as effects and blenders.
/// </summary>
public class ListCommand : ICommand
{
    public string Name { get { return "list"; } }
    public string Description { get { return "[effects|blenders]  lists wall properties"; } }

    public string Execute(string[] args, CommandRegistry registry)
    {
        Controller controller = registry.Controller;
        if (args.Length > 0)
        {
            if (args[0] == "effects")
            {
                EffectBase[] effects = controller.effects;
                var lines = new List<string> { "Available effects:" };
                foreach (var eff in effects)
                {
                    lines.Add(eff.Name);
                }
                return string.Join("\r\n", lines);
            }
            if (args[0] == "blenders")
            {
                BlenderBase[] blenders = controller.blenders;
                TransitionBase[] transitions = controller.transitions;
                var lines = new List<string> { "Available blenders:" };
                foreach (var eff in blenders)
                {
                    lines.Add(eff.Name + " " + eff.Usage());
                }
                foreach (var eff in transitions)
                {
                    lines.Add(eff.Name + " " + eff.Usage());
                }
                return string.Join("\r\n", lines);
            }
        }
        return "error";
    }
}


/// <summary>
/// Telnet command that jumps directly to a named effect.
/// </summary>
public class EffectCommand : ICommand
{
    public string Name { get { return "effect"; } }
    public string Description { get { return "<name> [time] run an effect for some amount of time"; } }

    public string Execute(string[] args, CommandRegistry registry)
    {
        Controller controller = registry.Controller;
        EffectBase[] effects = controller.effects;

        if (args.Length > 0)
            for (int i = 0; i < effects.Length; i++)
            {
                if (effects[i].Name == args[0])
                {
                    float time = controller.effectTime;
                    if (args.Length > 1)
                        time = float.Parse(args[1]);
                    controller.JumpToEffect(i, time);
                    return "";
                }
            }

        return "Effect '" + args[0] + "' not found.";
    }
}

/// <summary>
/// Telnet command that selects an external-source blender or transition blender.
/// </summary>
public class BlenderCommand : ICommand
{
    public string Name { get { return "blender"; } }
    public string Description { get { return "[name] [values] use a blend between native and ACN sources"; } }

    public string Execute(string[] args, CommandRegistry registry)
    {
        Controller controller = registry.Controller;
        BlenderBase[] blenders = controller.blenders;
        TransitionBase[] transitions = controller.transitions;

        if (args.Length > 0)
        {
            for (int i = 0; i < blenders.Length; i++)
            {
                if (blenders[i].Name == args[0])
                {
                    controller.ActiveTransitionBlender = null;
                    controller.ActiveBlender = blenders[i];
                    controller.ActiveBlender.setFaders(args.Skip(1).ToArray());
                    return "";
                }
            }
            for (int i = 0; i < transitions.Length; i++)
            {
                if (transitions[i].Name == args[0])
                {
                    controller.ActiveBlender = null;
                    controller.ActiveTransitionBlender = transitions[i];
                    controller.ActiveTransitionBlender.setFaders(args.Skip(1).ToArray());
                    return "";
                }
            }

        }

        return "Blender '" + args[0] + "' not found.";
    }
}


#if PREP_CAPTURE
/// <summary>
/// Telnet command for PREP_CAPTURE dummy input control.
/// </summary>
public class DummyCommand : ICommand
{
    public string Name { get { return "dummy"; } }
    public string Description { get { return "[on|off]] turn dummy blend source on and off"; } }

    public string Execute(string[] args, CommandRegistry registry)
    {
        Controller controller = registry.Controller;
        if (args[0] == "on")
        {
            controller.dummyActive = true;
            return "";
        }
        if (args[0] == "off")
        {
            controller.dummyActive = false;
            return "";
        }
        return "error";
    }
}
#endif

/// <summary>
/// Telnet command that toggles the NYE random-white overlay mode.
/// </summary>
public class NYECommand : ICommand
{
    public string Name { get { return "nye"; } }
    public string Description { get { return "[on|off]] turn nye effect on and off"; } }

    public string Execute(string[] args, CommandRegistry registry)
    {
        Controller controller = registry.Controller;
        if (args[0] == "on")
        {
            controller.NYE = true;
        }
        if (args[0] == "off")
        {
            controller.NYE = false;
        }
        return "error";
    }
}


/// <summary>
/// Per-client telnet connection wrapper and line parser.
/// </summary>
public class ClientConnection
{
    public TcpClient TcpClient;
    public NetworkStream Stream;
    public ConcurrentQueue<string> IncomingQueue = new ConcurrentQueue<string>();
    public ConcurrentQueue<string> OutgoingQueue = new ConcurrentQueue<string>();
}



/// <summary>
/// Optional telnet command server for runtime inspection and control. Inactive unless ENABLE_TELNET is defined.
/// </summary>
public class TelnetServer : MonoBehaviour
{
    List<ClientConnection> clients = new List<ClientConnection>();
    readonly object clientLock = new object();  // For thread-safe access to `clients`    public Controller controller;
    public int port = 23; // Default Telnet port
    private TcpListener listener;
    private bool isListening = false;
    private Thread listenThread;
    CommandRegistry registry;

    private readonly System.Collections.Generic.List<TcpClient> connectedClients = new System.Collections.Generic.List<TcpClient>();

    //private readonly object clientLock = new object();

    /// <summary>
    /// Initializes command registrations and starts the telnet listener thread.
    /// </summary>
    public void Start()
    {
        if (controller == null)
        {
            throw new InvalidOperationException("TelnetServer requires the live Controller before Start().");
        }

        StartTelnetServer();
        registry = new CommandRegistry(controller);
        registry.Register(new HelpCommand());
        registry.Register(new EchoCommand());
        registry.Register(new ListCommand());
        registry.Register(new EffectCommand());
        registry.Register(new BlenderCommand());
#if PREP_CAPTURE
        registry.Register(new DummyCommand());
#endif
        registry.Register(new NYECommand());


    }

    /// <summary>
    /// Stops the telnet server when the MonoBehaviour is destroyed.
    /// </summary>
    void OnDestroy()
    {
        StopTelnetServer();
    }

    /// <summary>
    /// Opens the TCP listener on port 23 and starts accepting clients on a background thread.
    /// </summary>
    void StartTelnetServer()
    {
        TcpListener listener = new TcpListener(IPAddress.Any, 23);
        listener.Start();
        Console.WriteLine("Server started.");

        new Thread(() =>
        {
            while (true)
            {
                TcpClient tcpClient = listener.AcceptTcpClient();
                var conn = new ClientConnection
                {
                    TcpClient = tcpClient,
                    Stream = tcpClient.GetStream()
                };

                lock (clientLock)
                {
                    clients.Add(conn);
                }

                new Thread(HandleClient).Start(conn);
            }
        }).Start();
    }


    /// <summary>
    /// Stops accepting telnet clients and closes the listener.
    /// </summary>
    void StopTelnetServer()
    {
        isListening = false;
        if (listener != null)
        {
            listener.Stop();
            listener = null;
        }

        // Disconnect all clients
        lock (clientLock)
        {
            foreach (var client in connectedClients)
            {
                client.Close();
            }
            connectedClients.Clear();
        }

        if (listenThread != null && listenThread.IsAlive)
        {
            listenThread.Join(); // Wait for the listener thread to finish
        }

        Debug.Log("Telnet server stopped.");
    }

    /// <summary>
    /// Blocking accept loop that starts one client handler thread per connection.
    /// </summary>
    void ListenForClients()
    {
        while (isListening)
        {
            try
            {
                TcpClient client = listener.AcceptTcpClient();
                Debug.Log($"Client connected: {client.Client.RemoteEndPoint}");
                lock (clientLock)
                {
                    connectedClients.Add(client);
                }
                Thread clientThread = new Thread(HandleClient);
                clientThread.IsBackground = true;
                clientThread.Start(client);
            }
            catch (SocketException e)
            {
                if (isListening) // Only log if the server is still supposed to be running
                {
                    Debug.LogError($"Socket error accepting client: {e.Message}");
                }
            }
            catch (Exception e)
            {
                if (isListening)
                {
                    Debug.LogError($"Error accepting client: {e.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Reads client command lines, executes registered commands, and writes responses.
    /// </summary>
    void HandleClient(object obj)
    {
        ClientConnection conn = (ClientConnection)obj;
        TcpClient client = conn.TcpClient;
        NetworkStream stream = conn.Stream;
        byte[] buffer = new byte[1024];

        byte[] welcomeBytes = Encoding.ASCII.GetBytes("Welcome to the Wall Server\r\n");
        stream.Write(welcomeBytes, 0, welcomeBytes.Length);
        stream.Flush();

        try
        {
            while (client.Connected)
            {
                // 🔹 1. Non-blocking read check
                if (stream.DataAvailable)
                {
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        string received = Encoding.ASCII.GetString(buffer, 0, bytesRead).Trim();
                        Console.WriteLine($"[HandleClient] Received: {received}");
                        conn.IncomingQueue.Enqueue(received);
                    }
                }

                // 🔹 2. Always check for and send outgoing messages
                while (conn.OutgoingQueue.TryDequeue(out string response))
                {
                    byte[] outBytes = Encoding.ASCII.GetBytes(response + "\r\n");
                    stream.Write(outBytes, 0, outBytes.Length);
                    Console.WriteLine($"[HandleClient] Sent: {response}");
                    stream.Flush();
                }

                Thread.Sleep(10); // Prevents CPU spin
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"[HandleClient] Exception: {e.Message}");
        }
        finally
        {
            lock (clientLock)
            {
                clients.Remove(conn);
            }
            client.Close();
        }
    }

    /// <summary>
    /// Main-thread service hook for queued command side effects and command logging.
    /// </summary>
    public void Service()
    {
        lock (clientLock)
        {
            foreach (var client in clients)
            {
                while (client.IncomingQueue.TryDequeue(out string cmd))
                {
                    System.IO.File.AppendAllText("log.txt", "in " + cmd);
                    if (string.IsNullOrWhiteSpace(cmd)) continue;

                    System.IO.File.AppendAllText("log.txt", $"[{DateTime.Now:HH:mm:ss}] IN:  {cmd}{Environment.NewLine}");
                    Console.WriteLine($"[Service] Processing: {cmd}");

                    string response = ProcessCommand(cmd);
                    client.OutgoingQueue.Enqueue(response);
                    System.IO.File.AppendAllText("log.txt", "out " + response);

                    System.IO.File.AppendAllText("log.txt", $"[{DateTime.Now:HH:mm:ss}] OUT: {response}{Environment.NewLine}");
                }
            }
        }
    }

    string ProcessCommand(string input)
    {

        if (string.IsNullOrWhiteSpace(input))
            return "";

        string[] tokens = input.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        string commandName = tokens[0];
        string[] args = tokens.Length > 1 ? tokens.Skip(1).ToArray() : new string[0];

        var command = registry.GetCommand(commandName);

        string result = command != null
            ? command.Execute(args, registry)
            : "Unknown command: " + commandName;

        return result;
    }
}
#endif
