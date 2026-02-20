using System.Diagnostics;
using System.Globalization;
using BojanGrujicSite.Models;

namespace BojanGrujicSite.Helpers
{
    public static class LinqExecutor
    {
        public static LinqResult Execute(List<LinqDynamic> items, string expression, List<LinqProperty> schema)
        {
            var result = new LinqResult
            {
                Expression = expression,
                Amount = items.Count,
                ColumnNames = schema.Select(p => p.Name).ToList()
            };

            try
            {
                result.MemoryBefore = GC.GetTotalMemory(false);

                var sw = Stopwatch.StartNew();
                var output = ExecuteChain(items, expression, schema);
                sw.Stop();

                result.MemoryAfter = GC.GetTotalMemory(false);
                result.ExecutionTime = sw.ElapsedMilliseconds;
                result.ExecutionTimeMicroseconds = Math.Round(sw.Elapsed.TotalMicroseconds, 2);
                result.ResultData = output;
                result.ResultCount = output.Count;
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                result.ExecutionTime = -1;
            }

            return result;
        }

        private static List<Dictionary<string, object>> ExecuteChain(
            List<LinqDynamic> items, string expression, List<LinqProperty> schema)
        {
            var operations = ParseOperations(expression);
            var typeMap = schema.ToDictionary(p => p.Name, p => p.Type, StringComparer.OrdinalIgnoreCase);

            IEnumerable<Dictionary<string, object>> current = items
                .Select(i => new Dictionary<string, object>(i.Properties, StringComparer.OrdinalIgnoreCase));

            foreach (var (method, args) in operations)
            {
                switch (method.ToLower())
                {
                    case "where":
                        var predicate = BuildPredicate(args, typeMap);
                        current = current.Where(predicate);
                        break;
                    case "orderby":
                        current = current.OrderBy(r => GetComparable(r, args.Trim(), typeMap));
                        break;
                    case "orderbydescending":
                        current = current.OrderByDescending(r => GetComparable(r, args.Trim(), typeMap));
                        break;
                    case "take":
                        if (int.TryParse(args.Trim(), out int takeCount))
                            current = current.Take(takeCount);
                        break;
                    case "skip":
                        if (int.TryParse(args.Trim(), out int skipCount))
                            current = current.Skip(skipCount);
                        break;
                    case "select":
                        var cols = args.Split(',').Select(c => c.Trim()).ToList();
                        current = current.Select(r =>
                        {
                            var d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                            foreach (var c in cols)
                                if (r.TryGetValue(c, out var v)) d[c] = v;
                            return d;
                        });
                        break;
                    case "count":
                        var count = current.Count();
                        return new List<Dictionary<string, object>> { new() { ["Count"] = count } };
                    case "first":
                        return new List<Dictionary<string, object>> { current.First() };
                    case "firstordefault":
                        var fod = current.FirstOrDefault();
                        return fod != null ? new List<Dictionary<string, object>> { fod } : new();
                    case "distinct":
                        current = current.Distinct(new DictComparer());
                        break;
                    case "reverse":
                        current = current.Reverse();
                        break;
                    case "any":
                        if (string.IsNullOrWhiteSpace(args))
                            return new List<Dictionary<string, object>> { new() { ["Any"] = current.Any() } };
                        var anyPred = BuildPredicate(args, typeMap);
                        return new List<Dictionary<string, object>> { new() { ["Any"] = current.Any(anyPred) } };
                    default:
                        throw new InvalidOperationException($"Unsupported LINQ method: .{method}()");
                }
            }

            return current.ToList();
        }

        private static IComparable? GetComparable(Dictionary<string, object> row, string prop, Dictionary<string, DataType> typeMap)
        {
            if (row.TryGetValue(prop, out var val) && val is IComparable c)
                return c;
            return null;
        }

        private static Func<Dictionary<string, object>, bool> BuildPredicate(string expr, Dictionary<string, DataType> typeMap)
        {
            // Support && and || by splitting into groups
            if (expr.Contains("&&") || expr.Contains("||"))
                return BuildCompoundPredicate(expr, typeMap);

            return BuildSingleCondition(expr.Trim(), typeMap);
        }

        private static Func<Dictionary<string, object>, bool> BuildCompoundPredicate(string expr, Dictionary<string, DataType> typeMap)
        {
            // Split on || first (lower precedence), then && within each group
            var orGroups = SplitRespectingParens(expr, "||");
            var orPredicates = orGroups.Select(group =>
            {
                var andParts = SplitRespectingParens(group.Trim(), "&&");
                var andPredicates = andParts.Select(p => BuildSingleCondition(p.Trim(), typeMap)).ToList();
                return new Func<Dictionary<string, object>, bool>(row => andPredicates.All(pred => pred(row)));
            }).ToList();

            return row => orPredicates.Any(pred => pred(row));
        }

        private static List<string> SplitRespectingParens(string input, string separator)
        {
            var parts = new List<string>();
            int depth = 0, start = 0;
            for (int i = 0; i < input.Length; i++)
            {
                if (input[i] == '(') depth++;
                else if (input[i] == ')') depth--;
                else if (depth == 0 && i + separator.Length <= input.Length && input.Substring(i, separator.Length) == separator)
                {
                    parts.Add(input[start..i]);
                    i += separator.Length - 1;
                    start = i + 1;
                }
            }
            parts.Add(input[start..]);
            return parts;
        }

        private static Func<Dictionary<string, object>, bool> BuildSingleCondition(string cond, Dictionary<string, DataType> typeMap)
        {
            // Handle .Contains(), .StartsWith(), .EndsWith()
            var methodMatch = System.Text.RegularExpressions.Regex.Match(cond,
                @"^(\w+)\.(Contains|StartsWith|EndsWith)\(""([^""]*)""\)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (methodMatch.Success)
            {
                string prop = methodMatch.Groups[1].Value;
                string method = methodMatch.Groups[2].Value.ToLower();
                string arg = methodMatch.Groups[3].Value;
                return row =>
                {
                    if (!row.TryGetValue(prop, out var val)) return false;
                    var s = val?.ToString() ?? "";
                    return method switch
                    {
                        "contains" => s.Contains(arg, StringComparison.OrdinalIgnoreCase),
                        "startswith" => s.StartsWith(arg, StringComparison.OrdinalIgnoreCase),
                        "endswith" => s.EndsWith(arg, StringComparison.OrdinalIgnoreCase),
                        _ => false
                    };
                };
            }

            // Handle negation: !condition or NOT condition
            if (cond.StartsWith("!") || cond.StartsWith("NOT ", StringComparison.OrdinalIgnoreCase))
            {
                var inner = cond.StartsWith("!") ? cond[1..].Trim() : cond[4..].Trim();
                if (inner.StartsWith("(") && inner.EndsWith(")"))
                    inner = inner[1..^1];
                var innerPred = BuildSingleCondition(inner, typeMap);
                return row => !innerPred(row);
            }

            // Parse comparison: Property op Value
            string[] operators = { ">=", "<=", "!=", "==", ">", "<" };
            foreach (var op in operators)
            {
                int idx = cond.IndexOf(op);
                if (idx < 0) continue;

                string propName = cond[..idx].Trim();
                string valueStr = cond[(idx + op.Length)..].Trim().Trim('"');

                if (!typeMap.TryGetValue(propName, out var dataType))
                    throw new InvalidOperationException($"Unknown property: '{propName}'");

                return row =>
                {
                    if (!row.TryGetValue(propName, out var val)) return false;
                    return CompareTyped(val, valueStr, op, dataType);
                };
            }

            // Handle bare bool: "IsActive" or property name alone
            if (typeMap.TryGetValue(cond, out var boolType) && boolType == DataType.Bool)
                return row => row.TryGetValue(cond, out var v) && v is bool b && b;

            throw new InvalidOperationException($"Cannot parse condition: '{cond}'");
        }

        private static bool CompareTyped(object val, string literal, string op, DataType type)
        {
            int cmp;
            switch (type)
            {
                case DataType.Int:
                    cmp = Convert.ToInt32(val).CompareTo(int.Parse(literal)); break;
                case DataType.Long:
                    cmp = Convert.ToInt64(val).CompareTo(long.Parse(literal)); break;
                case DataType.Short:
                    cmp = Convert.ToInt16(val).CompareTo(short.Parse(literal)); break;
                case DataType.Byte:
                    cmp = Convert.ToByte(val).CompareTo(byte.Parse(literal)); break;
                case DataType.Double:
                    cmp = Convert.ToDouble(val).CompareTo(double.Parse(literal, CultureInfo.InvariantCulture)); break;
                case DataType.Float:
                    cmp = Convert.ToSingle(val).CompareTo(float.Parse(literal, CultureInfo.InvariantCulture)); break;
                case DataType.Decimal:
                    cmp = Convert.ToDecimal(val).CompareTo(decimal.Parse(literal, CultureInfo.InvariantCulture)); break;
                case DataType.String:
                    cmp = string.Compare(val?.ToString(), literal, StringComparison.OrdinalIgnoreCase); break;
                case DataType.Bool:
                    cmp = Convert.ToBoolean(val).CompareTo(bool.Parse(literal)); break;
                case DataType.Char:
                    cmp = Convert.ToChar(val).CompareTo(literal[0]); break;
                case DataType.DateTime:
                    cmp = Convert.ToDateTime(val).CompareTo(DateTime.Parse(literal, CultureInfo.InvariantCulture)); break;
                default:
                    cmp = string.Compare(val?.ToString(), literal, StringComparison.OrdinalIgnoreCase); break;
            }

            return op switch
            {
                "==" => cmp == 0,
                "!=" => cmp != 0,
                ">" => cmp > 0,
                "<" => cmp < 0,
                ">=" => cmp >= 0,
                "<=" => cmp <= 0,
                _ => false
            };
        }

        private class DictComparer : IEqualityComparer<Dictionary<string, object>>
        {
            public bool Equals(Dictionary<string, object>? x, Dictionary<string, object>? y)
            {
                if (x == null || y == null) return x == y;
                return x.Count == y.Count && x.All(kvp => y.TryGetValue(kvp.Key, out var v) && Equals(kvp.Value, v));
            }
            public int GetHashCode(Dictionary<string, object> obj) =>
                obj.Aggregate(0, (h, kvp) => h ^ (kvp.Key.GetHashCode() ^ (kvp.Value?.GetHashCode() ?? 0)));
        }

        private static List<(string Method, string Args)> ParseOperations(string expression)
        {
            var ops = new List<(string, string)>();
            var input = expression.Trim();

            if (input.StartsWith("."))
                input = input[1..];

            int i = 0;
            while (i < input.Length)
            {
                int parenStart = input.IndexOf('(', i);
                if (parenStart < 0) break;

                string method = input[i..parenStart].Trim().TrimStart('.');

                int depth = 1;
                int j = parenStart + 1;
                while (j < input.Length && depth > 0)
                {
                    if (input[j] == '(') depth++;
                    else if (input[j] == ')') depth--;
                    j++;
                }

                string args = input[(parenStart + 1)..(j - 1)].Trim();
                ops.Add((method, args));

                i = j;
                if (i < input.Length && input[i] == '.')
                    i++;
            }

            if (ops.Count == 0)
                throw new InvalidOperationException(
                    "Could not parse expression. Use format like: .Where(Id > 30).OrderBy(Name)");

            return ops;
        }
    }
}
