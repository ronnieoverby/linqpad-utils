<Query Kind="Program" />

void Main()
{
	var graphs = LoadCsprojNodes(@"D:\Code\mideo")
		.Where(x => !string.IsNullOrWhiteSpace(x.Id));
	
	FindDistinctCircularPaths(graphs)
		.Select(c => string.Join(" → ", c))
		.Dump();
}

IEnumerable<Node> LoadCsprojNodes(string rootPath)
{
	var di = new DirectoryInfo(rootPath);
	var projs = di.GetFiles("*.csproj", SearchOption.AllDirectories);
	var nodes = projs.Select(f => FromCsproj(f.FullName)).Distinct().ToDictionary(n => n.Id);
	
	foreach (var node in nodes.Values.ToArray())
	{
		for (int i = 0; i < node.Refs.Count; i++)
		{
			var r = node.Refs[i];

			if (nodes.TryGetValue(r.Id, out Node r2))				
				node.Refs[i] = r2;
			else
				nodes[r.Id] = r;
		}
	}
	
	return nodes.Values;
}

Node FromCsproj(string projPath)
{
	var xml = XDocument.Load(projPath);

	XNamespace ns = "http://schemas.microsoft.com/developer/msbuild/2003";

	var node = new Node((string)xml.Descendants(ns + "AssemblyName").FirstOrDefault());

	var refs = from e in xml.Descendants(ns + "Reference")
			   let include = e.Attribute("Include")
			   where include != null
			   let id = getId((string)include)
			   select new Node(id);

	node.Refs.AddRange(refs);

	return node;

	string getId(string include) =>
		string.Concat(include.TakeWhile(c => c != ','));
}

class Node
{
	private static int _seq;

	public string Id { get; }

	public List<Node> Refs { get; } = new List<Node>();

	public Node() : this($"node#{Interlocked.Increment(ref _seq)}")
	{

	}

	public Node(string id)
	{
		Id = id ?? "";
	}

	public IEnumerable<(bool circular, IEnumerable<string> path)> Travel()
	{
		return travel(new string[0], this);

		IEnumerable<(bool circular, IEnumerable<string> path)> travel(IEnumerable<string> path, Node node)
		{
			var list = new List<string>(path);
			var circular = list.Contains(node.Id);
			list.Add(node.Id);

			if (circular)
			{
				yield return (true, list);
				yield break;
			}

			if (node.Refs.Any())
				foreach (var r in node.Refs)
					foreach (var t in travel(list, r))
						yield return t;
			else
				yield return (false, list);
		}
	}

	public override string ToString() => 
		Id ?? "";
		
	public override bool Equals(object obj) =>
		obj is Node node ? node.Id == Id : false;

	public override int GetHashCode() => 
		ToString().GetHashCode();
}

IEnumerable<IEnumerable<string>> FindDistinctCircularPaths(IEnumerable<Node> nodes)
{
	var q = from n in nodes.AsParallel()
			from t in n.Travel()
			where t.circular
			orderby t.path.Count()
			select t.path;

	var list = new List<(string key, IEnumerable<string> circle)>();
	var uniq = Guid.NewGuid().ToString();

	foreach (var circle in q)
	{
		var key = getKey(circle);
		if (!list.Any(t => key.Contains(t.key)))
		{
			// new circle
			list.Add((key, circle));
		}
	}

	return from t in list
		   let c = normalize(t.circle)
		   group c by getKey(c) into g
		   select g.First();

	string getKey(IEnumerable<string> s) =>
		string.Join(uniq, s);

	IEnumerable<T> normalize<T>(IEnumerable<T> path)
	{
		var l = path as IList<T> ?? new List<T>(path);
		return circular().Skip(l.IndexOf(l.Min())).Take(l.Count);

		IEnumerable<T> circular()
		{
			while (true)
			{
				for (int i = 0; i < l.Count - 1; i++)
					yield return l[i];
			}
		}
	}
}