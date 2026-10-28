using Godot;

public partial class GameManager : Node
{	
	public static GameManager Instance { get; set; }

	public float MouseSensitivity {get; set;} = 0.003f;
	public float StickSensitivity {get; set;} = 2.5f;

	public override void _Ready() {
		Instance = this;
	}
}
