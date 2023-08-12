// This line creates a new instance, and wraps the instance in a using statement so it's automatically disposed once we've exited the block.

using recreate_nrw;

using var window = new Window();
window.Run();