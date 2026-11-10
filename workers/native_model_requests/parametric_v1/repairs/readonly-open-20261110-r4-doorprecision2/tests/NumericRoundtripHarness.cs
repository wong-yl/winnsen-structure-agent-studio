using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

internal static class ParametricContext
{
    /*__JSON__*/
    /*__READ__*/
    /*__NUMBER__*/

    private static int Main(string[] args)
    {
        var input = Read(args[0]);
        var values = new List<double>();
        var boxedTypes = new Dictionary<string, int>();
        foreach (object item in (IEnumerable)input["values"])
        {
            var row = (Dictionary<string, object>)item;
            string type = row["value"].GetType().FullName;
            if (!boxedTypes.ContainsKey(type)) boxedTypes[type] = 0;
            boxedTypes[type]++;
            values.Add(Number(row, "value"));
        }
        var controls = new Dictionary<string, double>();
        controls["int"] = Number(new Dictionary<string, object> { { "value", 123 } }, "value");
        controls["long"] = Number(new Dictionary<string, object> { { "value", 2147483648L } }, "value");
        controls["double"] = Number(new Dictionary<string, object> { { "value", 123.75d } }, "value");
        File.WriteAllText(args[1], Json.Serialize(new { values = values, boxedTypes = boxedTypes, controls = controls }), new UTF8Encoding(false));
        Console.WriteLine("NUMERIC_ROUNDTRIP_RESULT " + Json.Serialize(new { count = values.Count, boxedTypes = boxedTypes }));
        return 0;
    }
}
