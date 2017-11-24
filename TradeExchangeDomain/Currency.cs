namespace TradeExchangeDomain
{
    public class Currency
    {
        protected bool Equals(Currency other)
        {
            return string.Equals(Name, other.Name);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Currency) obj);
        }

        public override int GetHashCode()
        {
            return (Name != null ? Name.GetHashCode() : 0);
        }

        public string Name { get; }

        private Currency(string name)
        {
            Name = name;
        }

        public static Currency Usd { get; } = new Currency("USD");
        public static Currency Btc { get; } = new Currency("BTC");
    }
}