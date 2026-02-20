using BojanGrujicSite.Models;

namespace BojanGrujicSite.Helpers
{
    public static class DataGenerator
    {
        private static readonly Random Rng = new();

        public static List<LinqDynamic> GenerateItems(List<LinqProperty> schema, int count)
        {
            var items = new List<LinqDynamic>(count);
            for (int i = 0; i < count; i++)
            {
                var item = new LinqDynamic();
                foreach (var prop in schema)
                {
                    item.AddProperty(prop.Name, GenerateValue(prop.Type, i));
                }
                items.Add(item);
            }
            return items;
        }

        public static object GenerateValue(DataType type, int index)
        {
            return type switch
            {
                DataType.Int => Rng.Next(-1000, 10000),
                DataType.String => RandomString(5 + Rng.Next(10)),
                DataType.Bool => Rng.Next(2) == 1,
                DataType.Double => Math.Round(Rng.NextDouble() * 10000 - 5000, 2),
                DataType.Float => (float)Math.Round(Rng.NextDouble() * 10000 - 5000, 2),
                DataType.Decimal => (decimal)Math.Round(Rng.NextDouble() * 10000 - 5000, 2),
                DataType.Char => (char)('A' + Rng.Next(26)),
                DataType.Byte => (byte)Rng.Next(256),
                DataType.Short => (short)Rng.Next(-1000, 1000),
                DataType.Long => (long)Rng.Next(-100000, 100000),
                DataType.DateTime => DateTime.Now.AddDays(Rng.Next(-3650, 3650)).AddHours(Rng.Next(24)),
                DataType.Object => $"obj_{index}",
                _ => $"unknown_{index}"
            };
        }

        private static string RandomString(int length)
        {
            const string chars = "abcdefghijklmnopqrstuvwxyz";
            var buffer = new char[length];
            for (int i = 0; i < length; i++)
                buffer[i] = chars[Rng.Next(chars.Length)];
            return new string(buffer);
        }
    }
}
