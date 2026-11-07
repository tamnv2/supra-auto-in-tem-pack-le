using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SupraAutoPackLe;

internal sealed record SkuWatchItem(string SKU, DateTime AddedAt, DateTime UpdatedAt);

/// <summary>Local, per-Windows-user watch list. No upload and no WMS writes.</summary>
internal sealed class SkuWatchStore
{
    private readonly string _path;
    private readonly object _gate = new();
    private static readonly Regex Format = new("^[A-Z0-9_-]{1,50}$", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public SkuWatchStore(string root) => _path = Path.Combine(root, "sku-watch.json");

    public static string Normalize(string value) => (value ?? "").Trim().ToUpperInvariant();
    public static bool Valid(string value) => Format.IsMatch(Normalize(value));

    public IReadOnlyList<SkuWatchItem> Items()
    {
        lock(_gate)
        {
            if(!File.Exists(_path)) return [];
            var rows = JsonSerializer.Deserialize<List<SkuWatchItem>>(File.ReadAllText(_path, Encoding.UTF8))
                ?? throw new InvalidDataException("Danh sách SKU không hợp lệ.");
            if(rows.Any(x=>!Valid(x.SKU)) ||
               rows.Select(x=>Normalize(x.SKU)).Distinct(StringComparer.Ordinal).Count()!=rows.Count)
                throw new InvalidDataException("Danh sách SKU có mã không hợp lệ hoặc trùng lặp.");
            return rows.OrderBy(x=>x.AddedAt).ToArray();
        }
    }

    public void Add(string value)
    {
        var sku = Normalize(value);
        if(!Valid(sku)) throw new ArgumentException("SKU phải gồm 1–50 ký tự A-Z, 0-9, _ hoặc -.");
        lock(_gate)
        {
            var rows = Items().ToList();
            if(rows.Any(x=>Normalize(x.SKU)==sku))
                throw new InvalidOperationException("SKU đã có trong danh sách.");
            var now=DateTime.Now;
            rows.Add(new SkuWatchItem(sku,now,now));
            Write(rows);
        }
    }

    public void Edit(string original, string replacement)
    {
        var sku=Normalize(replacement);
        if(!Valid(sku)) throw new ArgumentException("SKU phải gồm 1–50 ký tự A-Z, 0-9, _ hoặc -.");
        lock(_gate)
        {
            var rows=Items().ToList();
            var i=rows.FindIndex(x=>Normalize(x.SKU)==Normalize(original));
            if(i<0) throw new InvalidOperationException("Không tìm thấy SKU để sửa.");
            if(rows.Where((_,index)=>index!=i).Any(x=>Normalize(x.SKU)==sku))
                throw new InvalidOperationException("SKU đã có trong danh sách.");
            rows[i]=rows[i] with { SKU=sku, UpdatedAt=DateTime.Now };
            Write(rows);
        }
    }

    public void Delete(string value)
    {
        lock(_gate)
        {
            var rows=Items().ToList();
            if(rows.RemoveAll(x=>Normalize(x.SKU)==Normalize(value))==0)
                throw new InvalidOperationException("SKU không còn trong danh sách.");
            Write(rows);
        }
    }

    private void Write(List<SkuWatchItem> rows)
    {
        var tmp=_path+".tmp";
        try
        {
            File.WriteAllText(tmp,JsonSerializer.Serialize(rows,Json),new UTF8Encoding(false));
            File.Move(tmp,_path,true);
        }
        finally { try { if(File.Exists(tmp)) File.Delete(tmp); } catch {} }
    }
}
