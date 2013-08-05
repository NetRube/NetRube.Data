using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace NetRube.Data.Internal
{
	internal static class ParametersHelper
	{
		// Helper to handle named parameters from object properties
		public static string ProcessParams(string sql, object[] args_src, List<object> args_dest)
		{
			return rxParams.Replace(sql, m =>
			{
				string param = m.Value.Substring(1);

				object arg_val;

				int paramIndex;
				if(int.TryParse(param, out paramIndex))
				{
					// Numbered parameter
					if(paramIndex < 0 || paramIndex >= args_src.Length)
						throw new ArgumentOutOfRangeException(Localization.Resource.SqlParamsOutOfRangeException.F(paramIndex, args_src.Length, sql));
					arg_val = args_src[paramIndex];
				}
				else
				{
					// Look for a property on one of the arguments with this name
					bool found = false;
					arg_val = null;
					foreach(var o in args_src)
					{
						var pi = o.GetType().GetProperty(param);
						if(pi != null)
						{
							arg_val = pi.GetValue(o, null);
							found = true;
							break;
						}
					}

					if(!found)
						throw new ArgumentException(Localization.Resource.SqlParamsNotFoundException.F(param, sql));
				}

				// Expand collections to parameter lists
				if((arg_val as System.Collections.IEnumerable) != null &&
					(arg_val as string) == null &&
					(arg_val as byte[]) == null)
				{
					var sb = new StringBuilder();
					foreach(var i in arg_val as System.Collections.IEnumerable)
					{
						sb.Append((sb.Length == 0 ? "@" : ",@") + args_dest.Count.ToString());
						args_dest.Add(i);
					}
					return sb.ToString();
				}
				else
				{
					args_dest.Add(arg_val);
					return "@" + (args_dest.Count - 1).ToString();
				}
			}
			);
		}

		static Regex rxParams = new Regex(@"(?<!@)@\w+", RegexOptions.Compiled);
	}
}