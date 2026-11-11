namespace Islet.Core.Bar;
/// <summary>Owns one registration. Hiding, failure and disposal share the same idempotent release boundary.</summary>
public sealed class BarReservation(Action remove):IDisposable
{
    public bool Active{get;private set;}
    public bool Acquire(Func<bool> register)=>Active||(Active=register());
    public void Dispose(){if(!Active)return;Active=false;remove();}
}
