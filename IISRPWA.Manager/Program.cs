using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IISRPWA.Manager
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "IISRPWA Manager";
            try
            {
                var actions = new Action[] { ListAll, ManageUsers, AddUser, ManageIPExceptions, AddIPException, ManagePathExceptions, AddPathException, SaveChanges, Exit };
                while (true)
                {

                    for (int i = 0; i < actions.Length; i++)
                    {
                        Console.WriteLine($"{i + 1}: {actions[i].Method.Name}");
                    }
                    var selectedAction = actions[int.Parse(Console.ReadLine()) - 1];
                    selectedAction();
                }
            }
            catch (Exception ex)
            {
                if (ex.Message == "Exit")
                {
                    return;
                }
                else
                {
                    Console.WriteLine(ex.ToString());
                }
            }
        }

        private static void Manage<T>(Action<T, Collection<T>>[] actions, Collection<T> items)
        {
            if (items.Count == 0)
            {
                Console.WriteLine($"No {typeof(T).Name.ToLower()}'s found");
                return;
            }
            while (true)
            {
                try
                {
                    for (int i = 0; i < items.Count; i++)
                    {
                        var item = items[i];
                        Console.WriteLine($"{i + 1}: {item}");
                    }
                    var input = Console.ReadLine();
                    if (string.IsNullOrEmpty(input))
                        return;
                    var selecteditem = items[int.Parse(input) - 1];

                    for (int i = 0; i < actions.Length; i++)
                    {
                        Console.WriteLine($"{i + 1}: {actions[i].Method.Name}");
                    }
                    input = Console.ReadLine();
                    if (string.IsNullOrEmpty(input))
                        return;
                    var selectedAction = actions[int.Parse(input) - 1];
                    selectedAction(selecteditem, items);
                }
                catch (Exception ex)
                {
                    if (ex.Message == "Exit")
                    {
                        return;
                    }
                    else
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
            }
        }

        private static void ManageUsers()
        {
            Manage(new Action<User, Collection<User>>[] { ChangeUsername, ChangePassword, DeleteUser, Exit }, Configuration.Instance.Users);
        }

        private static void ManageIPExceptions()
        {
            Manage(new Action<IPException, Collection<IPException>>[] { ChangeIPException, DeleteIPException, Exit }, Configuration.Instance.IPExceptions);
        }

        private static void ManagePathExceptions()
        {
            Manage(new Action<PathException, Collection<PathException>>[] { ChangePathException, DeletePathException, Exit }, Configuration.Instance.PathExceptions);
        }

        private static void AddUser()
        {
            var user = new User();
            try
            {
                ChangeUsername(user, Configuration.Instance.Users);
                ChangePassword(user, Configuration.Instance.Users);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            Configuration.Instance.Users.Add(user);
        }

        private static void ChangeUsername(User user, Collection<User> users)
        {

            Console.Write("Username: ");
            var username = Console.ReadLine();
            if (string.IsNullOrEmpty(username))
                throw new Exception("Username is required");
            if (users.Any(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase) && user != u))
                throw new Exception("User with that username already exists");
            user.Username = username;
        }

        private static void ChangePassword(User user, Collection<User> users)
        {
            Console.Write("Password: ");
            var password = PasswordInput();
            if (password.Length < 7)
                throw new Exception("Password to short");
            Console.Write("Confirm password: ");
            var confirmPassword = PasswordInput();
            if (password != confirmPassword)
                throw new Exception("Password mismatch!");
            Console.Write("Hash iterations: ");
            var hashIterations = int.Parse(Console.ReadLine());
            Console.Write("Salt size: ");
            var saltSize = int.Parse(Console.ReadLine());
            var salt = HashHelper.GenerateSalt(saltSize);
            var passwordData = Encoding.UTF8.GetBytes(password);
            var watch = Stopwatch.StartNew();
            var hash = HashHelper.CalculateHash(passwordData, salt, hashIterations);
            watch.Stop();
            Console.WriteLine($"Hash calculated in {watch.Elapsed}.");
            user.HashIterations = hashIterations;
            user.PasswordSalt = Convert.ToBase64String(salt);
            user.PasswordHash = Convert.ToBase64String(hash);
        }

        private static void DeleteUser(User user, Collection<User> users)
        {
            users.Remove(user);
        }

        private static void AddIPException()
        {
            var ipException = new IPException();
            ChangeIPException(ipException, Configuration.Instance.IPExceptions);
            Configuration.Instance.IPExceptions.Add(ipException);
        }

        private static void ChangeIPException(IPException ipException, Collection<IPException> ipExceptions)
        {
            Console.Write("Pattern: ");
            var pattern = Console.ReadLine();
            if (string.IsNullOrEmpty(pattern))
                throw new Exception("Pattern can't be empty");
            ipException.Pattern = pattern;
        }

        private static void DeleteIPException(IPException ipException, Collection<IPException> ipExceptions)
        {
            ipExceptions.Remove(ipException);
        }

        private static void AddPathException()
        {
            var pathException = new PathException();
            ChangePathException(pathException, Configuration.Instance.PathExceptions);
            Configuration.Instance.PathExceptions.Add(pathException);
        }

        private static void ChangePathException(PathException pathException, Collection<PathException> pathExceptions)
        {
            Console.Write("Pattern: ");
            var pattern = Console.ReadLine();
            if (string.IsNullOrEmpty(pattern))
                throw new Exception("Pattern can't be empty");
            pathException.Pattern = pattern;
        }

        private static void DeletePathException(PathException pathException, Collection<PathException> pathExceptions)
        {
            pathExceptions.Remove(pathException);
        }

        public static void SaveChanges()
        {
            Configuration.Instance.SaveChanges();
        }

        private static void Exit()
        {
            //Ugly solution to exit application but works!
            throw new Exception("Exit");
        }

        private static void Exit<T>(T item, Collection<T> items)
        {
            //Ugly solution to exit application but works!
            throw new Exception("Exit");
        }

        private static void ListAll()
        {
            Console.WriteLine("Users");
            foreach (var user in Configuration.Instance.Users)
                Console.WriteLine("- " + user);
            Console.WriteLine("IPExceptions");
            foreach (var ipException in Configuration.Instance.IPExceptions)
                Console.WriteLine("- " + ipException);
            Console.WriteLine("PathExceptions");
            foreach (var pathException in Configuration.Instance.PathExceptions)
                Console.WriteLine("- " + pathException);
        }

        static string PasswordInput()
        {
            string password = null;
            while (true)
            {
                var key = System.Console.ReadKey(true);
                if (key.Key == ConsoleKey.Enter)
                    break;
                else if (key.Key == ConsoleKey.Backspace)
                {
                    if (password != null && password.Length > 0)
                    {
                        password = password.Substring(0, password.Length - 1);
                    }
                }
                else
                {
                    password += key.KeyChar;
                }
            }
            Console.WriteLine();
            return password;
        }
    }
}
