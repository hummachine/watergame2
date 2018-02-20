using System.Collections.Generic;
using System.Text;
using System.Linq;

public class ShaderVariant
{
	private Dictionary<string, bool> unityKeywords;
	private Dictionary<string, bool> waterKeywords;
	private Dictionary<string, string> shaderParts;

	public ShaderVariant()
	{
		unityKeywords = new Dictionary<string, bool>();
		waterKeywords = new Dictionary<string, bool>();
		shaderParts = new Dictionary<string, string>();
    }

	public void SetUnityKeyword(string keyword, bool value)
	{
		if(value)
			unityKeywords[keyword] = true;
		else
			unityKeywords.Remove(keyword);
    }

	public void SetWaterKeyword(string keyword, bool value)
	{
		if(value)
			waterKeywords[keyword] = value;
		else
			waterKeywords.Remove(keyword);
	}

	public void SetAdditionalCode(string keyword, string code)
	{
		if(code != null)
			shaderParts[keyword] = code;
		else
			shaderParts.Remove(keyword);
    }

	public bool IsUnityKeywordEnabled(string keyword)
	{
		bool value;

		if(unityKeywords.TryGetValue(keyword, out value))
			return true;

		return false;
	}

	public bool IsWaterKeywordEnabled(string keyword)
	{
		bool value;

		if(waterKeywords.TryGetValue(keyword, out value))
			return true;

		return false;
	}

	public string GetAdditionalCode()
	{
		StringBuilder sb = new StringBuilder(512);
		
		foreach(string code in shaderParts.Values)
			sb.Append(code);

		return sb.ToString();
	}

	public string[] GetUnityKeywords()
	{
		string[] keywords = new string[unityKeywords.Count];
		int index = 0;

		foreach(string keyword in unityKeywords.Keys)
			keywords[index++] = keyword;

		return keywords;
	}

	public string[] GetWaterKeywords()
	{
		string[] keywords = new string[waterKeywords.Count];
		int index = 0;

		foreach(string keyword in waterKeywords.Keys)
			keywords[index++] = keyword;

		return keywords;
	}

	public string GetKeywordsString()
	{
		StringBuilder sb = new StringBuilder(512);
		bool notFirst = false;

		foreach(string keyword in waterKeywords.Keys.OrderBy(k => k))
		{
			if(notFirst)
				sb.Append(' ');
			else
				notFirst = true;

			sb.Append(keyword);
		}

		foreach(string keyword in unityKeywords.Keys.OrderBy(k => k))
		{
			if(notFirst)
				sb.Append(' ');
			else
				notFirst = true;

			sb.Append(keyword);
		}

		foreach(string keyword in shaderParts.Keys.OrderBy(k => k))
		{
			if(notFirst)
				sb.Append(' ');
			else
				notFirst = true;

			sb.Append(keyword);
		}

		return sb.ToString();
	}
}
