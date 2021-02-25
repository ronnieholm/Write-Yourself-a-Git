using System.Collections.Specialized;

namespace WriteYourselfAGit.Core
{
    public static class KeyValueListWithMessageParser
    {
        public static void Parse(byte[] raw)
        {
            var o = new OrderedDictionary {{"c", "x"}, {"a", "y"}, {"b", "z"}};

            foreach (var x in o.Keys)
                System.Console.WriteLine($"{x} : {o[x]}");
        }
    }
}