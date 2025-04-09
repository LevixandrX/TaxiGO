using BCrypt.Net;

namespace TaxiGO
{
    public static class GenerateHashes
    {
        public static void Generate()
        {
            var passwords = new (string Login, string Password)[]
            {
                ("client1", "hash123"),
                ("client2", "hash456"),
                ("client3", "hash789"),
                ("client4", "hash101"),
                ("client5", "hash102"),
                ("driver1", "hash200"),
                ("driver2", "hash201"),
                ("driver3", "hash202"),
                ("driver4", "hash203"),
                ("driver5", "hash204"),
                ("admin1", "hash300"),
                ("admin2", "hash301"),
                ("disp1", "hash400"),
                ("client6", "hash103"),
                ("client7", "hash104"),
                ("client8", "hash105"),
                ("driver6", "hash205"),
                ("client9", "hash106"),
                ("client10", "hash107"),
                ("driver7", "hash206")
            };

            foreach (var (login, password) in passwords)
            {
                string hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
                System.Diagnostics.Debug.WriteLine($"UPDATE Users SET PasswordHash = '{hashedPassword}' WHERE Login = '{login}';");
            }
        }
    }
}