using System.Dynamic;

namespace BojanGrujicSite.Models
{
    public class LinqDynamic : DynamicObject
    {
        private readonly Dictionary<string, object> _dictionary = new();

        public object this[string propertyName]
        {
            get => _dictionary[propertyName];
            set => AddProperty(propertyName, value);
        }

        public IReadOnlyDictionary<string, object> Properties => _dictionary;

        public override bool TryGetMember(GetMemberBinder binder, out object? result)
        {
            return _dictionary.TryGetValue(binder.Name, out result);
        }

        public override bool TrySetMember(SetMemberBinder binder, object? value)
        {
            AddProperty(binder.Name, value!);
            return true;
        }

        public override IEnumerable<string> GetDynamicMemberNames() => _dictionary.Keys;

        public void AddProperty(string name, object value)
        {
            _dictionary[name] = value;
        }

        public void RemoveProperty(string name)
        {
            _dictionary.Remove(name);
        }
    }
}
