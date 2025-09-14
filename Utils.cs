using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace weland;
public static class Utils
{
    public static T JsonClone<T>(this T item)
    {
        return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(item, JsonSerializerOptions.Web), JsonSerializerOptions.Web)!;
    }
}
