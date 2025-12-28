using System.Text.Json;

namespace PcStore.Web.Extensions
{
    public static class SessionExtensions
    {
        // Метод записи объекта в сессию
        public static void Set<T>(this ISession session, string key, T value)
        {
            session.SetString(key, JsonSerializer.Serialize(value));
        }

        // Метод получения объекта из сессии
        public static T? Get<T>(this ISession session, string key)
        {
            var value = session.GetString(key);
            return value == null ? default : JsonSerializer.Deserialize<T>(value);
        }
    }
}