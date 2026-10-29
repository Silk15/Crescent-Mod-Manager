namespace Crescent.Utilities
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class HintAttribute : Attribute
    {
        public string Hint { get; set; }
        public string TextHex { get; set; }

        public HintAttribute(string hint, string textHex = "#F1F1F1")
        {
            Hint = hint;
            TextHex = textHex;
        }
    }
}