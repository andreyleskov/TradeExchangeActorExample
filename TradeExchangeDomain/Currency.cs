namespace TradeExchangeDomain
{
    public class Currency
    {
        private Currency(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public static Currency Usd { get; } = new Currency("USD");
        public static Currency Btc { get; } = new Currency("BTC");

        protected bool Equals(Currency other)
        {
            return string.Equals(Name, other.Name);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((Currency) obj);
        }

        public override int GetHashCode()
        {
            return Name != null ? Name.GetHashCode() : 0;
        }
    }
}