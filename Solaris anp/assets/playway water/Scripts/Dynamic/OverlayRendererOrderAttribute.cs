using System;

public class OverlayRendererOrderAttribute : Attribute
{
	private int priority;

	public OverlayRendererOrderAttribute(int priority)
	{
		this.priority = priority;
	}

	public int Priority
	{
		get { return priority; }
	}
}
