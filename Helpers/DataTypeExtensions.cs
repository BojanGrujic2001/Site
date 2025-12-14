namespace BojanGrujicSite.Helpers
{
    public static class DataTypeExtensions
    {
        public static Type ToSystemType(this DataType dataType)
        {
            return dataType switch
            {
                DataType.Int => typeof(int),
                DataType.String => typeof(string),
                DataType.Bool => typeof(bool),
                DataType.Double => typeof(double),
                DataType.Float => typeof(float),
                DataType.Decimal => typeof(decimal),
                DataType.Char => typeof(char),
                DataType.Byte => typeof(byte),
                DataType.Short => typeof(short),
                DataType.Long => typeof(long),
                DataType.Object => typeof(object),
                DataType.DateTime => typeof(DateTime),
                _ => typeof(object)
            };
        }
    }
}
