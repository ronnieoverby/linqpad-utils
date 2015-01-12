<Query Kind="Program" />

void Main()
{
	var leftPath = @"C:\Users\roverby\Desktop\transformed.xml";
	var rightPath = @"C:\Users\roverby\Desktop\backedup.xml";

	var xmlLeft = XDocument.Load(leftPath);
	var xmlRight = XDocument.Load(rightPath);
	
	new{Left = leftPath, Right = rightPath}.Dump("Config Paths");
	
	CompareAppSettings(xmlLeft,xmlRight);
	CompareConnStrings(xmlLeft,xmlRight);	
}

void CompareAppSettings(XDocument xmlLeft, XDocument xmlRight)
{
	var leftSettings =  from s in xmlLeft.Descendants("appSettings").Single().Elements()
					   select new { isLeft = true, element = s };
	var rightSettings =  from s in xmlRight.Descendants("appSettings").Single().Elements()
					   select new { isLeft = false, element = s };
					   
	var allSettings =  from s in leftSettings.Concat(rightSettings)
						let key = s.element.Attribute("key")
						let value = s.element.Attribute("value")
						where key != null && value != null
						select new { s.isLeft, key = key.Value, value = value.Value };
	
	var comparison = from g in allSettings.GroupBy(x=>x.key)
					let l = g.SingleOrDefault(x => x.isLeft)
					let r = g.SingleOrDefault(x => !x.isLeft)
					let lv = l == null ? null : l.value
					let rv = r == null ? null : r.value
					 let comp = new 
					{
						Name = g.Key,
						Left = lv,
						Right = rv,
						Match = lv == rv,
					}
					orderby comp.Name
					select comp;
						
	comparison.Dump("appSettings");
	
	var conflicts = comparison.Where (x => x.Left != x.Right && x.Left != null && x.Right != null).ToArray()
		.Dump("conflicting appSettings");
		
	new XElement("appSettings",
		comparison.Except(conflicts).Select (x => 
			new XElement("add",
				new XAttribute("key", x.Name),
				new XAttribute("value", x.Left ?? x.Right)			
			)
		)
	).Dump(string.Format("merged appSettings (except {0} conflicts)",conflicts.Length));	
}




void CompareConnStrings(XDocument xmlLeft, XDocument xmlRight)
{
	var leftSettings =  from s in xmlLeft.Descendants("connectionStrings").Single().Elements()
					   select new { isLeft = true, element = s };
	var rightSettings =  from s in xmlRight.Descendants("connectionStrings").Single().Elements()
					   select new { isLeft = false, element = s };
					   
	var allSettings =  from s in leftSettings.Concat(rightSettings)
						let key = s.element.Attribute("name")
						let value = s.element.Attribute("connectionString")
						where key != null && value != null
						select new { s.isLeft, key = key.Value, value = value.Value };
	
	var comparison = from g in allSettings.GroupBy(x=>x.key)
					let l = g.SingleOrDefault(x => x.isLeft)
					let r = g.SingleOrDefault(x => !x.isLeft)
					let lv = l == null ? null : l.value
					let rv = r == null ? null : r.value
					 let comp = new 
					{
						Name = g.Key,
						Left = lv,
						Right = rv,
						Match = lv == rv,
					}
					orderby comp.Name
					select comp;
						
	comparison.Dump("connectionStrings");
	
	var conflicts = comparison.Where (x => x.Left != x.Right && x.Left != null && x.Right != null).ToArray()
		.Dump("conflicting connectionStrings");
		
	new XElement("connectionString",
		comparison.Except(conflicts).Select (x => 
			new XElement("add",
				new XAttribute("name", x.Name),
				new XAttribute("connectionString", x.Left ?? x.Right)			
			)
		)
	).Dump(string.Format("merged connectionStrings (except {0} conflicts)",conflicts.Length));	
}