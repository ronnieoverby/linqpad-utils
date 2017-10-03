<Query Kind="Program">
  <NuGetReference>Newtonsoft.Json</NuGetReference>
  <Namespace>Newtonsoft.Json.Linq</Namespace>
  <Namespace>System.Security.Cryptography</Namespace>
</Query>

// first connect this query to your octopus database	

// run the script and you'll be prompted for the master key
// to find master key, use powershell on your octopus server:
// PS C:\Windows\system32> & 'C:\Program Files\Octopus Deploy\Octopus\Octopus.Server.exe' show-master-key
// if you cant't get your master key then you have no business looking at the variables

void Main()
{
	var pwkey = $"octopus master key {Connection.ConnectionString.GetHashCode()}";
	var masterKey = Convert.FromBase64String(Util.GetPassword(pwkey));
	var projects = Projects.ToArray();
	var projName = Util.ReadLine("Project? (Arrow up/down for suggestions)", projects[0].Name, projects.Select(x => x.Name));
	var project = projects.First(p => p.Name == projName);
	var releases = Releases.Where(x => x.ProjectId == project.Id).ToArray();
	var versions = releases.OrderByDescending(x => Version.Parse(x.Version)).Select(x => x.Version).ToArray();
	var version = Util.ReadLine("Release? (Arrow up/down for suggestions)", versions[0], versions);
	var release = releases.Single(x => x.Version == version);
	var vset = VariableSets.Single(x => x.Id == release.ProjectVariableSetSnapshotId);

	var jq = from v in JObject.Parse(vset.JSON)["Variables"]
			 let type = v.Value<string>("Type")
			 where type == "Sensitive"
			 let value = (string)v["Value"]
			 select new
			 {
				 name = (string)v["Name"],
				 value = DecodeSensitiveVariable(value, masterKey),
				 scope = v["Scope"].ToString()
			 };

	jq.ToArray().Dump($"Sensitive Variables ({project.Name} v{version})");
}

string DecodeSensitiveVariable(string encodedValue, byte[] key)
{
	var parts = encodedValue.Split('|');
	var cipher = Convert.FromBase64String(parts[0]);
	var salt = Convert.FromBase64String(parts[1]);
	
	using (var algorithm = new AesCryptoServiceProvider
	{
		Padding = PaddingMode.PKCS7,
		KeySize = 128,
		Key = key,
		BlockSize = 128,
		Mode = CipherMode.CBC,
		IV = salt
	})
	using (var memoryStream = new MemoryStream())
	using (var decryptor = algorithm.CreateDecryptor())
	using (var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Write))
	{
		cryptoStream.Write(cipher, 0, cipher.Length);
		cryptoStream.FlushFinalBlock();
		var s = Encoding.UTF8.GetString(memoryStream.ToArray());
		return s;
	}
}
