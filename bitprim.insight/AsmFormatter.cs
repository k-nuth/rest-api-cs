using System.Collections.Generic;

namespace bitprim.insight
{
    /// <summary>
    /// Helper class for formatting asm scripts (for display)
    /// </summary>
    public class AsmFormatter
    {
        private static readonly Dictionary<string, string> tokenDictionary_;

        static AsmFormatter()
        {
            tokenDictionary_ = new Dictionary<string, string>
            {
                ["checksig"] = "OP_CHECKSIG",
                ["dup"] = "OP_DUP",
                ["equal"] = "OP_EQUAL",
                ["equalverify"] = "OP_EQUALVERIFY",
                ["hash160"] = "OP_HASH160",
                ["return"] = "OP_RETURN"
            };
        }

        /// <summary>
        /// Format script string for display.
        /// </summary>
        /// <param name="script"> Script string (not raw). </param>
        public string Format(string script)
        {
            var formatted = "";
            int tokenEnd = -1;
            var keepParsing = true;
            while(keepParsing)
            {
                int tokenStart = tokenEnd + 1;
                tokenEnd = script.IndexOf(' ', tokenStart);
                if(tokenEnd < 0) //Last token
                {
                    tokenEnd = script.Length;
                    keepParsing = false;
                }
                string token = script.Substring(tokenStart, tokenEnd-tokenStart);
                bool replace = tokenDictionary_.TryGetValue(token, out string tokenReplacement);

                if (replace)
                {
                    formatted += tokenReplacement + " ";
                }
                else
                {
                    if (token.StartsWith("[") && token.EndsWith("]"))
                    {
                        formatted += token.Substring(1, token.Length - 2) + " ";
                    }
                    else
                    {
                        formatted += token + " ";
                    }
                }
                    
            }
            formatted = formatted.TrimEnd();
            return formatted;
        }
    }
}