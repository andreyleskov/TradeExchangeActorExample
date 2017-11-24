namespace TradeExchangeDomain
{
    public class Symbol
    {
        private readonly string _name;

        public Symbol(Currency @base, Currency target)
        {
            Base = @base;
            Target = target;
            _name = Base.ToString() + Target;
        }

        public Currency Base { get; }
        public Currency Target { get; }

        public static Symbol UsdBtc { get; } = new Symbol(Currency.Usd, Currency.Btc);

        public override bool Equals(object obj)
        {
            return (obj as Symbol)?._name == _name;
        }

        protected bool Equals(Symbol other)
        {
            return string.Equals(_name, other._name);
        }

        public override int GetHashCode()
        {
            return _name != null ? _name.GetHashCode() : 0;
        }

        public override string ToString()
        {
            return _name;
        }
    }
}