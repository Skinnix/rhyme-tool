//using System.Reflection;
//using Skinnix.RhymeTool.Data.Notation.IO;

//using (var test = Assembly.GetExecutingAssembly().GetManifestResourceStream("Skinnix.RhymeTool.Test.Resources.everybody-hurts.cho")!)
//{
//	var reader = ChordProSheetDecoderReader.Default;
//	var sheet = reader.ReadSheet(test);

//	var writer = ChordProSheetEncoderWriter.Default;
//	using (var ms = new MemoryStream())
//	{
//		writer.WriteSheet(ms, sheet, true);
//		ms.Position = 0;
//		var content = new StreamReader(ms).ReadToEnd();
//	}
//}
