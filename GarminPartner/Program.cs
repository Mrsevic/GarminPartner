using GarminPartner.Core.Services;

namespace GarminPartner;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("🏃 Garmin Partner - Forerunner 165 Workout Manager\n");

        var authService = new GarminAuthService();
        var workoutService = new GarminWorkoutService(authService);

        // Check for existing authentication
        var authData = await authService.GetValidAuthAsync();
        
        if (authData == null)
        {
            Console.WriteLine("🔑 No valid authentication found. Please login:\n");
            
            Console.Write("Email: ");
            var email = Console.ReadLine();
            
            Console.Write("Password: ");
            var password = ReadPassword();
            
            Console.WriteLine();
            
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                Console.WriteLine("❌ Email and password are required");
                return;
            }

            authData = await authService.AuthenticateAsync(email, password);
            
            if (authData == null)
            {
                Console.WriteLine("❌ Authentication failed. Please check your credentials.");
                return;
            }
        }
        else
        {
            Console.WriteLine("✅ Using existing authentication\n");
        }

        // Menu
        while (true)
        {
            Console.WriteLine("\n--- Menu ---");
            Console.WriteLine("1. Send Simple 5K Run Workout");
            Console.WriteLine("2. Clear Authentication");
            Console.WriteLine("3. Exit");
            Console.Write("\nSelect option: ");
            
            var choice = Console.ReadLine();

            switch (choice)
            {
                case "1":
                    Console.WriteLine();
                    await workoutService.SendSimpleRunWorkoutAsync();
                    break;
                    
                case "2":
                    authService.ClearAuth();
                    Console.WriteLine("✅ Authentication cleared. Restart to login again.");
                    return;
                    
                case "3":
                    Console.WriteLine("\n👋 Goodbye!");
                    return;
                    
                default:
                    Console.WriteLine("❌ Invalid option");
                    break;
            }
        }
    }

    private static string ReadPassword()
    {
        var password = string.Empty;
        ConsoleKeyInfo key;

        do
        {
            key = Console.ReadKey(true);

            if (key.Key != ConsoleKey.Backspace && key.Key != ConsoleKey.Enter)
            {
                password += key.KeyChar;
                Console.Write("*");
            }
            else if (key.Key == ConsoleKey.Backspace && password.Length > 0)
            {
                password = password[0..^1];
                Console.Write("\b \b");
            }
        }
        while (key.Key != ConsoleKey.Enter);

        return password;
    }
}