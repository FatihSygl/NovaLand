namespace NovaIsland.Models;
public static class HomeSelection
{
    public static TodoEntry? Task(IEnumerable<TodoEntry> tasks, Guid? selected, bool sessionRunning)
    {
        var active = tasks.FirstOrDefault(x => x.Id == selected && !x.IsCompleted);
        return active ?? (sessionRunning ? null : tasks.FirstOrDefault(x => !x.IsCompleted));
    }
}
