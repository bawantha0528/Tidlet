using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Islet.Core.Activities;
using Islet.Windows.Media;

namespace Islet.Windows.Tests;

/// <summary>Synthetic native ABI fixture: commands call only these unmanaged mock callbacks.</summary>
[TestClass]
public sealed unsafe class NativeMediaAbiTests
{
    [TestMethod]
    public void SnapshotReadsOwnedStringsByteBooleansTimesAndReleasesEveryReference()
    {
        using var fixture = new Fixture();
        using (var backend = fixture.Backend())
        {
            var value = backend.Read(CancellationToken.None);
            Assert.AreEqual(MediaState.Available,value.State);
            Assert.AreEqual("Mock title",value.Title); Assert.AreEqual("Mock artist",value.Subtitle);
            Assert.IsTrue(value.Playing); Assert.IsTrue(value.CanToggle); Assert.IsTrue(value.CanNext); Assert.IsTrue(value.CanPrevious);
            Assert.AreEqual(TimeSpan.FromSeconds(20),value.Position); Assert.AreEqual(TimeSpan.FromMinutes(3),value.Duration);
        }
        fixture.AssertBalanced();
    }
    [TestMethod]
    public void NativeCommandChecksIdentityAndTokenBeforeCallingOnlyMockPlayback()
    {
        using var fixture = new Fixture();
        using (var backend = fixture.Backend())
        {
            var value=backend.Read(CancellationToken.None);
            Assert.IsFalse(backend.Execute(MediaCommand.TogglePlayback,"stale",CancellationToken.None).Success);
            Assert.AreEqual(0,fixture.Commands);
            Assert.IsTrue(backend.Execute(MediaCommand.TogglePlayback,value.SessionToken!,CancellationToken.None).Success);
            Assert.IsTrue(backend.Execute(MediaCommand.Previous,value.SessionToken!,CancellationToken.None).Success);
            Assert.IsTrue(backend.Execute(MediaCommand.Next,value.SessionToken!,CancellationToken.None).Success);
            Assert.AreEqual(3,fixture.Commands);
            fixture.Manager->Result=0;
            Assert.IsFalse(backend.Execute(MediaCommand.TogglePlayback,value.SessionToken!,CancellationToken.None).Success);
            Assert.AreEqual(3,fixture.Commands);
        }
        fixture.AssertBalanced();
    }
    [TestMethod]
    public void AsyncFailureReleasesOperationWithoutIssuingAnyPlaybackCommand()
    {
        using var fixture=new Fixture(); fixture.Status=3;
        using(var backend=fixture.Backend())
            Assert.ThrowsExactly<UnauthorizedAccessException>(()=>backend.Read(CancellationToken.None));
        Assert.AreEqual(0,fixture.Commands); fixture.AssertBalanced();
    }
    [TestMethod]
    public void PendingAsyncReadRequestsCancellationAndReleasesAllOwnedReferences()
    {
        using var fixture=new Fixture(); fixture.Status=0;
        using var cancel=new CancellationTokenSource(TimeSpan.FromMilliseconds(60));
        using(var backend=fixture.Backend())
            Assert.ThrowsExactly<OperationCanceledException>(()=>backend.Read(cancel.Token));
        Assert.AreEqual(1,fixture.CancelCalls); Assert.AreEqual(0,fixture.Commands); fixture.AssertBalanced();
    }    private struct Node { public nint Vtable; public int References, Kind, Commands; public nint Result, Extra; }
    private sealed class Fixture : IDisposable
    {
        private readonly List<nint> _nodes = new();
        public Node* Manager;
        private Node* _session, _properties, _operation, _info, _playback, _controls, _timeline, _boolean;
        public int Commands => _session->Commands;
        public int Status { set => _info->Commands=value; }
        public int CancelCalls => (int)_info->Result;
        public Fixture()
        {
            Manager=Create(1); _session=Create(2); _properties=Create(3); _operation=Create(4); _info=Create(5);
            _playback=Create(6); _controls=Create(7); _timeline=Create(8); _boolean=Create(9);
            Manager->Result=(nint)_session; _session->Result=(nint)_operation; _session->Extra=(nint)_playback;
            _operation->Result=(nint)_properties; _operation->Extra=(nint)_info; _boolean->Extra=(nint)_info;
            _playback->Result=(nint)_controls;
            Slot(Manager,6,(nint)(delegate* unmanaged[Stdcall]<nint,nint*,int>)&Result);
            Slot(_session,7,(nint)(delegate* unmanaged[Stdcall]<nint,nint*,int>)&Result);
            Slot(_session,8,(nint)(delegate* unmanaged[Stdcall]<nint,nint*,int>)&Timeline);
            Slot(_session,9,(nint)(delegate* unmanaged[Stdcall]<nint,nint*,int>)&Extra);
            foreach(var index in new[]{10,11,16,17,20}) Slot(_session,index,(nint)(delegate* unmanaged[Stdcall]<nint,nint*,int>)&Command);
            Slot(_operation,8,(nint)(delegate* unmanaged[Stdcall]<nint,nint*,int>)&Result);
            Slot(_boolean,8,(nint)(delegate* unmanaged[Stdcall]<nint,byte*,int>)&True);
            _info->Commands=1;
            Slot(_info,8,(nint)(delegate* unmanaged[Stdcall]<nint,int*,int>)&ErrorCode);
            Slot(_info,9,(nint)(delegate* unmanaged[Stdcall]<nint,int>)&Cancel);
            Slot(_info,7,(nint)(delegate* unmanaged[Stdcall]<nint,int*,int>)&Completed);
            Slot(_playback,6,(nint)(delegate* unmanaged[Stdcall]<nint,nint*,int>)&Result);
            Slot(_playback,7,(nint)(delegate* unmanaged[Stdcall]<nint,int*,int>)&Playing);
            foreach(var index in new[]{6,7,12,13,16}) Slot(_controls,index,(nint)(delegate* unmanaged[Stdcall]<nint,byte*,int>)&True);
            Slot(_properties,6,(nint)(delegate* unmanaged[Stdcall]<nint,nint*,int>)&Title);
            Slot(_properties,9,(nint)(delegate* unmanaged[Stdcall]<nint,nint*,int>)&Artist);
            Slot(_timeline,6,(nint)(delegate* unmanaged[Stdcall]<nint,long*,int>)&Start);
            Slot(_timeline,7,(nint)(delegate* unmanaged[Stdcall]<nint,long*,int>)&End);
            Slot(_timeline,10,(nint)(delegate* unmanaged[Stdcall]<nint,long*,int>)&Position);
            // Test-only pointers remain in unmanaged storage on this test thread.
            _session->Commands=0; _properties->Extra=(nint)_timeline; _controls->Extra=(nint)_boolean;
        }
        public NativeMediaSession Backend()
        {
            var value=new NativeMediaSession(); Manager->References++;
            typeof(NativeMediaSession).GetField("_manager",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(value,(nint)Manager);
            return value;
        }
        public void AssertBalanced() { foreach(var node in _nodes) Assert.AreEqual(1,((Node*)node)->References,$"Kind {((Node*)node)->Kind}"); }
        private Node* Create(int kind)
        {
            var node=(Node*)NativeMemory.AllocZeroed((nuint)sizeof(Node));
            node->Vtable=(nint)NativeMemory.AllocZeroed(32,(nuint)sizeof(nint)); node->References=1;node->Kind=kind;_nodes.Add((nint)node);
            Slot(node,0,(nint)(delegate* unmanaged[Stdcall]<nint,Guid*,nint*,int>)&Query);
            Slot(node,1,(nint)(delegate* unmanaged[Stdcall]<nint,uint>)&AddRef);
            Slot(node,2,(nint)(delegate* unmanaged[Stdcall]<nint,uint>)&Release);return node;
        }
        private static void Slot(Node* owner,int slot,nint value) => ((nint*)owner->Vtable)[slot]=value;
        public void Dispose() { foreach(var address in _nodes) { var node=(Node*)address; NativeMemory.Free((void*)node->Vtable);NativeMemory.Free(node); } }
    }
    private static int Return(nint address,nint* result) { *result=address;if(address!=0)((Node*)address)->References++;return 0; }
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])] private static int Query(nint owner,Guid* iid,nint* result) => Return(((Node*)owner)->Kind is 4 or 9 ? ((Node*)owner)->Extra : owner,result);
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])] private static uint AddRef(nint owner)=>(uint)++((Node*)owner)->References;
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])] private static uint Release(nint owner)=>(uint)--((Node*)owner)->References;
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])] private static int Result(nint owner,nint* result)=>Return(((Node*)owner)->Result,result);
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])] private static int Extra(nint owner,nint* result)=>Return(((Node*)owner)->Extra,result);
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])] private static int Timeline(nint owner,nint* result) { var properties=(Node*)((Node*)((Node*)owner)->Result)->Result; return Return(properties->Extra,result); }
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])] private static int Command(nint owner,nint* result) { ((Node*)owner)->Commands++;var controls=(Node*)((Node*)((Node*)owner)->Extra)->Result;return Return(controls->Extra,result); }
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])] private static int Completed(nint owner,int* value) { *value=((Node*)owner)->Commands;return 0; }
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])] private static int Playing(nint owner,int* value) { *value=4;return 0; }
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])] private static int True(nint owner,byte* value) { *value=1;return 0; }
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])] private static int Title(nint owner,nint* value)=>WindowsCreateString("Mock title",10,value);
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])] private static int Artist(nint owner,nint* value)=>WindowsCreateString("Mock artist",11,value);
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])] private static int Start(nint owner,long* value) { *value=0;return 0; }
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])] private static int End(nint owner,long* value) { *value=TimeSpan.TicksPerSecond*180;return 0; }
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])] private static int Position(nint owner,long* value) { *value=TimeSpan.TicksPerSecond*20;return 0; }
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])] private static int ErrorCode(nint owner,int* value) { *value=unchecked((int)0x80070005);return 0; }
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvStdcall)])] private static int Cancel(nint owner) { ((Node*)owner)->Result++;return 0; }    [DllImport("combase.dll",ExactSpelling=true,CharSet=CharSet.Unicode)] private static extern int WindowsCreateString(string source,uint length,nint* value);
}