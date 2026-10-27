
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.IO;
using System.Threading.Tasks;

class GameMessage
{
    public string Type { get; set; } = "";
    public string Name { get; set; } = "";
    public int[] Board { get; set; } = new int[9];
    public string Status { get; set; } = "";
    public string Winner { get; set; } = "";
}

class Program
{
    static UdpClient client = null!;
    static IPEndPoint remote = null!;
    static int player;
    static string myName = "";
    static string opponentName = "";
    static int[] board = new int[9];

    static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;

        Console.Write("Oyunçu seç (1 = X, 2 = O): ");
        if (!int.TryParse(Console.ReadLine(), out player) ||
            (player != 1 && player != 2))
        {
            Console.WriteLine("1 və ya 2 seç.");
            return;
        }

        Console.Write("Adını daxil et: ");
        myName = Console.ReadLine() ?? "";

        Console.Write("Rəqibin IP ünvanı: ");
        string ip = Console.ReadLine() ?? "";

        int localPort = player == 1 ? 5001 : 5002;
        int remotePort = player == 1 ? 5002 : 5001;

        if (!IPAddress.TryParse(ip, out IPAddress? address))
        {
            Console.WriteLine("IP ünvanı yanlışdır.");
            return;
        }

        client = new UdpClient(localPort);
        remote = new IPEndPoint(address, remotePort);

        Console.WriteLine("Rəqib gözlənilir...");
        Connect();

        Console.WriteLine($"Rəqib: {opponentName}");

        while (true)
        {
            if (player == 1)
            {
                ShowBoard();
                board[GetMove()] = 1;

                string status = GetStatus();
                SendMessage(new GameMessage
                {
                    Type = "GAME",
                    Board = board,
                    Status = status,
                    Winner = status == "WIN" ? myName : ""
                });

                if (status != "CONTINUE")
                {
                    ShowBoard();
                    SaveGame(status);
                    ShowResult(status);
                    break;
                }

                ReceiveGame();
                ShowBoard();

                status = GetStatus();

                if (status != "CONTINUE")
                {
                    SaveGame(status);
                    ShowResult(status);
                    break;
                }
            }
            else
            {
                ReceiveGame();
                ShowBoard();

                string status = GetStatus();

                if (status != "CONTINUE")
                {
                    SaveGame(status);
                    ShowResult(status);
                    break;
                }

                board[GetMove()] = 2;
                status = GetStatus();

                SendMessage(new GameMessage
                {
                    Type = "GAME",
                    Board = board,
                    Status = status,
                    Winner = status == "WIN" ? myName : ""
                });

                if (status != "CONTINUE")
                {
                    ShowBoard();
                    SaveGame(status);
                    ShowResult(status);
                    break;
                }
            }
        }

        client.Close();
    }

    static void Connect()
    {
        while (true)
        {
            SendMessage(new GameMessage
            {
                Type = "HELLO",
                Name = myName
            });

            GameMessage message = ReceiveMessage();

            if (message.Type == "HELLO")
            {
                opponentName = message.Name;
                return;
            }
        }
    }

    static void SendMessage(GameMessage message)
    {
        byte[] data = Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(message));

        client.Send(data, data.Length, remote);
    }

    static GameMessage ReceiveMessage()
    {
        IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);

        byte[] data = client.Receive(ref sender);

        return JsonSerializer.Deserialize<GameMessage>(
            Encoding.UTF8.GetString(data))!;
    }

    static void ReceiveGame()
    {
        GameMessage message = ReceiveMessage();

        if (message.Type != "GAME")
            throw new InvalidOperationException("Mesaj yanlışdır.");

        board = message.Board;
    }

    static void ShowBoard()
    {
        Console.WriteLine();

        for (int i = 0; i < 9; i++)
        {
            string value = board[i] == 1 ? "X" :
                           board[i] == 2 ? "O" :
                           (i + 1).ToString();

            Console.Write($" {value} ");

            if (i % 3 != 2)
                Console.Write("|");
            else if (i != 8)
                Console.WriteLine("\n---+---+---");
        }

        Console.WriteLine("\n");
    }

    static int GetMove()
    {
        while (true)
        {
            Console.Write($"{myName}, xana seç (1-9): ");

            if (int.TryParse(Console.ReadLine(), out int move) &&
                move >= 1 && move <= 9 && board[move - 1] == 0)
            {
                return move - 1;
            }

            Console.WriteLine("Boş xana seç.");
        }
    }

    static string GetStatus()
    {
        int[,] lines =
        {
            { 0, 1, 2 }, { 3, 4, 5 }, { 6, 7, 8 },
            { 0, 3, 6 }, { 1, 4, 7 }, { 2, 5, 8 },
            { 0, 4, 8 }, { 2, 4, 6 }
        };

        for (int i = 0; i < 8; i++)
        {
            int a = lines[i, 0];
            int b = lines[i, 1];
            int c = lines[i, 2];

            if (board[a] != 0 &&
                board[a] == board[b] &&
                board[b] == board[c])
                return "WIN";
        }

        return Array.Exists(board, x => x == 0)
            ? "CONTINUE" : "DRAW";
    }

    static void SaveGame(string status)
    {
        string json = JsonSerializer.Serialize(new
        {
            Gamer1 = player == 1 ? myName : opponentName,
            Gamer2 = player == 2 ? myName : opponentName,
            Board = board,
            Status = status
        }, new JsonSerializerOptions { WriteIndented = true });

        File.WriteAllText(
            $"{myName}_Game.json", json);

        Console.WriteLine("Oyun JSON faylına yazıldı.");
    }

    static void ShowResult(string status)
    {
        if (status == "WIN")
        {
            int winnerMark = board[0] == 1 ? 1 : 0;

            int[,] lines =
            {
                { 0, 1, 2 }, { 3, 4, 5 }, { 6, 7, 8 },
                { 0, 3, 6 }, { 1, 4, 7 }, { 2, 5, 8 },
                { 0, 4, 8 }, { 2, 4, 6 }
            };

            for (int i = 0; i < 8; i++)
            {
                int a = lines[i, 0];
                int b = lines[i, 1];
                int c = lines[i, 2];

                if (board[a] != 0 &&
                    board[a] == board[b] &&
                    board[b] == board[c])
                {
                    winnerMark = board[a];
                    break;
                }
            }

            string winner = winnerMark == (player == 1 ? 1 : 2)
                ? myName : opponentName;

            Console.WriteLine($"Qalib: {winner}");
        }
        else
        {
            Console.WriteLine("Heç-heçə!");
        }

        Console.WriteLine("Oyun bitdi!");
    }
}