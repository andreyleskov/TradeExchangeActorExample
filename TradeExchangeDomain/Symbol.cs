namespace TradeExchangeDomain
{
    public class Symbol
    {
        private readonly string _name;
        public Currency Base { get; }
        public Currency Target { get; }

        public Symbol(Currency @base, Currency target)
        {
            Base = @base;
            Target = target;
            _name = Base.ToString() + Target.ToString();
        }

        public override string ToString() => _name;

        public static Symbol UsdBtc { get; } = new Symbol(Currency.Usd, Currency.Btc);
    }
}