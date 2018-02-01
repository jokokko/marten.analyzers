namespace Marten.Analyzers.Infrastructure
{
    public sealed class RuleCategory
    {
        public string Name { get; }

        public static readonly RuleCategory Usage = new RuleCategory("Usage");		
        private RuleCategory(string name)
        {
            Name = name;
        }

        public static implicit operator string(RuleCategory value)
        {
            return value.Name;
        }
        private bool Equals(RuleCategory other)
        {
            return string.Equals(Name, other.Name);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is RuleCategory tag && Equals(tag);
        }

        public override int GetHashCode()
        {
            return (Name != null ? Name.GetHashCode() : 0);
        }
    }
}