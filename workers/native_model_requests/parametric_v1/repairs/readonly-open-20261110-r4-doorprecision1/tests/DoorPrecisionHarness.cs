using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace Winnsen.StructureAgent.Generation
{
    internal static class ParametricContext
    {
        internal static Dictionary<string, object> Plan;
        /*__OBJECT__*/
        /*__NUMBER__*/
        /*__REQUIRE__*/
    }

    internal static class NativeParametricDoor
    {
        internal static double GeneratedHeight;
        internal static void Generate(double height, string evidence) { GeneratedHeight = height; }
    }

    internal static class NativeParametricAssembly
    {
        /*__METHODS__*/

        internal static int DispatchDoor(string phase)
        {
            bool doorPhase = phase.StartsWith("door-", StringComparison.Ordinal);
            string evidence = "fixture-evidence.json";
            /*__DISPATCH__*/
            return -1;
        }

        internal static double DirectoryHeight(string directory)
        {
            /*__DIRECTORY__*/
            return height;
        }
    }
}

internal static class DoorPrecisionHarness
{
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
    private static readonly List<object> Cases = new List<object>();

    private static Dictionary<string, object> Map(object item) { return (Dictionary<string, object>)item; }
    private static void Require(bool result, string message) { if (!result) throw new InvalidOperationException(message); }

    private static void Check(string name, string expectedFailure, Action action)
    {
        try
        {
            action();
            Require(expectedFailure == null, "Expected failure: " + expectedFailure);
            Cases.Add(new { name = name, status = "PASS" });
        }
        catch (Exception error)
        {
            if (expectedFailure != null && error.Message == expectedFailure)
                Cases.Add(new { name = name, status = "PASS", expectedFailure = expectedFailure, observedFailure = error.Message });
            else Cases.Add(new { name = name, status = "FAIL", error = error.ToString() });
        }
    }

    private static int Main(string[] args)
    {
        var fixture = Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(args[0]));
        foreach (object raw in (IEnumerable)fixture["valid"])
        {
            var value = Map(raw);
            string name = (string)value["name"];
            var plan = Map(value["plan"]);
            var modules = ((IEnumerable)plan["doorModules"]).Cast<Dictionary<string, object>>().ToArray();
            Winnsen.StructureAgent.Generation.ParametricContext.Plan = plan;
            Check(name + ":module-coverage-and-deduplication", null, delegate {
                Require(Winnsen.StructureAgent.Generation.NativeParametricAssembly.ContractDoorHeights().Count == modules.Length, "MODULE_COUNT_CHANGED");
            });
            foreach (var module in modules)
            {
                string token = (string)module["token"];
                double height = Convert.ToDouble(module["heightMm"], CultureInfo.InvariantCulture);
                Check(name + ":resolve-full-height:" + token, null, delegate {
                    Require(Winnsen.StructureAgent.Generation.NativeParametricAssembly.ResolveContractDoorHeight(token) == height, "RESOLVED_HEIGHT_CHANGED");
                });
                Check(name + ":height-to-identifier:" + token, null, delegate {
                    Require(Winnsen.StructureAgent.Generation.NativeParametricAssembly.DoorHeightToken(height) == token, "IDENTIFIER_CHANGED");
                });
                Check(name + ":dispatch-full-height:" + token, null, delegate {
                    Winnsen.StructureAgent.Generation.NativeParametricAssembly.DispatchDoor("door-" + token);
                    Require(Winnsen.StructureAgent.Generation.NativeParametricDoor.GeneratedHeight == height, "GENERATION_HEIGHT_CHANGED");
                });
                Check(name + ":directory-full-height:" + token, null, delegate {
                    Require(Winnsen.StructureAgent.Generation.NativeParametricAssembly.DirectoryHeight("H" + token) == height, "PLACEMENT_HEIGHT_CHANGED");
                });
            }
            Check(name + ":unknown-phase-rejected", "PARAMETRIC_DOOR_HEIGHT_NOT_IN_CONTRACT", delegate {
                Winnsen.StructureAgent.Generation.NativeParametricAssembly.DispatchDoor("door-999999");
            });
            Check(name + ":unknown-height-rejected", "PARAMETRIC_DOOR_HEIGHT_NOT_IN_CONTRACT", delegate {
                Winnsen.StructureAgent.Generation.NativeParametricAssembly.DoorHeightToken(999999);
            });
            Check(name + ":invalid-directory-rejected", "PARAMETRIC_DOOR_DIRECTORY_INVALID", delegate {
                Winnsen.StructureAgent.Generation.NativeParametricAssembly.DirectoryHeight("wrong");
            });
        }
        foreach (object raw in (IEnumerable)fixture["invalid"])
        {
            var value = Map(raw);
            Winnsen.StructureAgent.Generation.ParametricContext.Plan = Map(value["plan"]);
            Check((string)value["name"], (string)value["failure"], delegate {
                Winnsen.StructureAgent.Generation.NativeParametricAssembly.ContractDoorHeights();
            });
        }
        int failed = Cases.Count(item => (string)item.GetType().GetProperty("status").GetValue(item, null) == "FAIL");
        Console.WriteLine("DOOR_PRECISION_TEST_RESULT " + Json.Serialize(new { passed = Cases.Count - failed, failed = failed, cases = Cases }));
        return failed == 0 ? 0 : 1;
    }
}
