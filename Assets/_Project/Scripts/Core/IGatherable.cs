namespace Ogni.Core
{
    /// <summary>
    /// Маркер: объект собирается на отдельную клавишу сбора ([F]), а не на общую клавишу взаимодействия ([E]).
    /// Так двери, машины и постройки остаются на E, а подбор ресурсов - на F.
    /// </summary>
    public interface IGatherable : IInteractable
    {
    }
}
