using FSO.SimAntics;
using FSO.SimAntics.Engine.TSOTransaction;
using FSO.SimAntics.Marshals;
using FSO.SimAntics.NetPlay.Drivers;
using FSO.SimAntics.NetPlay.Model;
using FSO.SimAntics.NetPlay.Model.Commands;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Simitone.Client.Utils
{
    /// <summary>
    /// Undo and redo for Buy and Build mode, as in the game. The lot is saved just before each buy or build command
    /// runs, and undo puts it back. While those modes are open nothing else on the lot moves, so a saved lot is exactly
    /// how it was before the change. The family's money isn't part of the lot, so it's kept alongside; the history is
    /// dropped once time runs again.
    /// </summary>
    public class UndoHistory
    {
        private const int Limit = 30;

        private class Step
        {
            public byte[] Lot;
            public int Budget;
        }

        private VM VM;
        private LinkedList<Step> Undos = new LinkedList<Step>();
        private Stack<Step> Redos = new Stack<Step>();

        public bool CanUndo => Undos.Count > 0;
        public bool CanRedo => Redos.Count > 0;

        public UndoHistory(VM vm)
        {
            VM = vm;
        }

        /// <summary>
        /// Called as a command is sent, before it runs.
        /// </summary>
        public void BeforeCommand(VMNetCommandBodyAbstract cmd)
        {
            if (VM.SpeedMultiplier >= 0) return;
            if (cmd is VMNetPlaceInventoryCmd || cmd is VMNetSendToInventoryCmd)
            {
                //the inventory is kept with the family, not the lot, so the lot alone can't be put back past this
                Clear();
                return;
            }
            if (!(cmd is VMNetBuyObjectCmd || cmd is VMNetMoveObjectCmd || cmd is VMNetDeleteObjectCmd
                || cmd is VMNetArchitectureCmd || cmd is VMNetSetRoofCmd)) return;
            //buying, selling and building are each sent twice: to pay, then again, verified, to make the change
            if ((cmd as VMNetArchitectureCmd)?.Verified == true || (cmd as VMNetBuyObjectCmd)?.Verified == true
                || (cmd as VMNetDeleteObjectCmd)?.Verified == true) return;
            Undos.AddLast(Save());
            if (Undos.Count > Limit) Undos.RemoveFirst();
            Redos.Clear();
        }

        public void Undo()
        {
            if (!CanUndo) return;
            var step = Undos.Last.Value;
            Undos.RemoveLast();
            Redos.Push(Save());
            Restore(step);
        }

        public void Redo()
        {
            if (!CanRedo) return;
            Undos.AddLast(Save());
            Restore(Redos.Pop());
        }

        public void Clear()
        {
            Undos.Clear();
            Redos.Clear();
        }

        /// <summary>
        /// Called every frame: forgets the history once time runs, as the lot then changes in ways undo can't follow.
        /// </summary>
        public void Update()
        {
            if (VM.SpeedMultiplier >= 0 && (CanUndo || CanRedo)) Clear();
        }

        private Step Save()
        {
            var marshal = VM.Save();
            //leave out what the player is holding: those are ghosts, and would come back as real objects
            var ghosts = new HashSet<short>(VM.Entities.Where(x => x.GhostImage).Select(x => x.ObjectID));
            if (ghosts.Count > 0)
            {
                var keep = Enumerable.Range(0, marshal.Entities.Length).Where(i => !ghosts.Contains(marshal.Entities[i].ObjectID)).ToArray();
                marshal.Entities = keep.Select(i => marshal.Entities[i]).ToArray();
                marshal.Threads = keep.Select(i => marshal.Threads[i]).ToArray();
                marshal.MultitileGroups = marshal.MultitileGroups.Where(x => !x.Objects.Any(ghosts.Contains)).ToArray();
            }
            using (var stream = new MemoryStream())
            {
                marshal.SerializeInto(new BinaryWriter(stream));
                return new Step { Lot = stream.ToArray(), Budget = VM.TS1State.CurrentFamily?.Budget ?? 0 };
            }
        }

        private void Restore(Step step)
        {
            var marshal = new VMMarshal();
            using (var reader = new BinaryReader(new MemoryStream(step.Lot))) marshal.Deserialize(reader);
            VM.Load(marshal);
            if (VM.TS1State.CurrentFamily != null) VM.TS1State.CurrentFamily.Budget = step.Budget;
        }
    }

    /// <summary>
    /// The local game's driver, telling the undo history about each command before it's queued.
    /// </summary>
    public class UndoDriver : VMServerDriver
    {
        public UndoHistory History;

        public UndoDriver(IVMTSOGlobalLink globalLink) : base(globalLink) { }

        public override void SendCommand(VMNetCommandBodyAbstract cmd)
        {
            History?.BeforeCommand(cmd);
            base.SendCommand(cmd);
        }
    }
}
