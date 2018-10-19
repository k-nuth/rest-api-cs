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
            tokenDictionary_ = new Dictionary<string, string>();
            tokenDictionary_["checksig"] = "OP_CHECKSIG";
            tokenDictionary_["dup"] = "OP_DUP";
            tokenDictionary_["equal"] = "OP_EQUAL";
            tokenDictionary_["equalverify"] = "OP_EQUALVERIFY";
            tokenDictionary_["hash160"] = "OP_HASH160";
            tokenDictionary_["return"] = "OP_RETURN";
        }

        /// <summary>
        /// Format script string for display.
        /// </summary>
        /// <param name="script"> Script string (not raw). </param>
        public string Format(string script)
        {
            string formatted = "";
            int tokenStart = 0;
            int tokenEnd = -1;
            bool keepParsing = true;
            while(keepParsing)
            {
                tokenStart = tokenEnd + 1;
                tokenEnd = script.IndexOf(' ', tokenStart);
                if(tokenEnd < 0) //Last token
                {
                    tokenEnd = script.Length;
                    keepParsing = false;
                }
                string token = script.Substring(tokenStart, tokenEnd-tokenStart);
                bool replace = tokenDictionary_.TryGetValue(token, out string tokenReplacement);
                formatted += replace? (tokenReplacement + " ") : token.Substring(1, token.Length - 2) + " ";
            }
            formatted = formatted.TrimEnd();
            return formatted;
        }
    }
}