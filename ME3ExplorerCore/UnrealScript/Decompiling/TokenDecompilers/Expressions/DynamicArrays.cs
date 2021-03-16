using ME3ExplorerCore.Packages;
using Unrealscript.Language.Tree;

namespace Unrealscript.Decompiling
{
    public partial class ByteCodeDecompiler
    {
        public DynArrayLength DecompileDynArrLength()
        {
            PopByte();
            var arr = DecompileExpression();
            if (arr == null)
                return null;

            StartPositions.Pop();
            return new DynArrayLength(arr);
        }

        public DynArrayRemove DecompileDynArrayRemove()
        {

            PopByte();
            var arr = DecompileExpression();
            if (arr == null)
                return null;
            var indexArg = DecompileExpression();
            if (indexArg == null)
                return null;
            var countArg = DecompileExpression();
            if (countArg == null)
                return null;
            if (Game >= MEGame.ME3)
            {
                PopByte(); //EndFuncParms
            }
            StartPositions.Pop();
            return new DynArrayRemove(arr, indexArg, countArg);
        }

        public DynArrayAdd DecompileDynArrayAdd()
        {

            PopByte();
            var arr = DecompileExpression();
            if (arr == null)
                return null;
            if (Game is MEGame.ME2)
            {
                ReadInt16(); // MemSize
            }
            var countArg = DecompileExpression();
            if (countArg == null)
                return null;
            if (Game >= MEGame.ME3)
            {
                PopByte(); //EndFuncParms
            }
            StartPositions.Pop();
            return new DynArrayAdd(arr, countArg);
        }

        public DynArrayInsert DecompileDynArrayInsert()
        {

            PopByte();
            var arr = DecompileExpression();
            if (arr == null)
                return null;
            var indexArg = DecompileExpression();
            if (indexArg == null)
                return null;
            var countArg = DecompileExpression();
            if (countArg == null)
                return null;
            if (Game >= MEGame.ME3)
            {
                PopByte(); //EndFuncParms
            }
            StartPositions.Pop();
            return new DynArrayInsert(arr, indexArg, countArg);
        }

        public DynArrayFindStructMember DecompileDynArrayFindStructMember()
        {

            PopByte();
            var arr = DecompileExpression();
            if (arr == null)
                return null;
            ReadInt16(); // MemSize
            var memberNameArg = DecompileExpression();
            if (memberNameArg == null)
                return null;
            var valueArg = DecompileExpression();
            if (valueArg == null)
                return null;
            if (Game >= MEGame.ME3)
            {
                PopByte(); //EndFuncParms
            }
            StartPositions.Pop();
            return new DynArrayFindStructMember(arr, memberNameArg, valueArg);
        }

        public DynArrayInsertItem DecompileDynArrayInsertItem()
        {

            PopByte();
            var arr = DecompileExpression();
            if (arr == null)
                return null;
            ReadInt16(); // MemSize
            var indexArg = DecompileExpression();
            if (indexArg == null)
                return null;
            var valueArg = DecompileExpression();
            if (valueArg == null)
                return null;
            if (Game >= MEGame.ME3)
            {
                PopByte(); //EndFuncParms
            }
            StartPositions.Pop();
            return new DynArrayInsertItem(arr, indexArg, valueArg);
        }

        public DynArrayFind DecompileDynArrayFind()
        {

            PopByte();
            var arr = DecompileExpression();
            if (arr == null)
                return null;
            ReadInt16(); // MemSize
            var valueArg = DecompileExpression();
            if (valueArg == null)
                return null;
            if (Game >= MEGame.ME3)
            {
                PopByte(); //EndFuncParms
            }
            StartPositions.Pop();
            return new DynArrayFind(arr, valueArg);
        }

        public DynArrayAddItem DecompileDynArrayAddItem()
        {

            PopByte();
            var arr = DecompileExpression();
            if (arr == null)
                return null;
            if (Game >= MEGame.ME2)
            {
                ReadInt16(); // MemSize
            }
            var valueArg = DecompileExpression();
            if (valueArg == null)
                return null;
            if (Game >= MEGame.ME3)
            {
                PopByte(); //EndFuncParms
            }
            StartPositions.Pop();
            return new DynArrayAddItem(arr, valueArg);
        }

        public DynArrayRemoveItem DecompileDynArrayRemoveItem()
        {

            PopByte();
            var arr = DecompileExpression();
            if (arr == null)
                return null;
            if (Game >= MEGame.ME2)
            {
                ReadInt16(); // MemSize
            }
            var valueArg = DecompileExpression();
            if (valueArg == null)
                return null;
            if (Game >= MEGame.ME3)
            {
                PopByte(); //EndFuncParms
            }
            StartPositions.Pop();
            return new DynArrayRemoveItem(arr, valueArg);
        }

        public DynArraySort DecompileDynArraySort()
        {

            PopByte();
            var arr = DecompileExpression();
            if (arr == null)
                return null;
            ReadInt16(); // MemSize
            var comparefunctionArg = DecompileExpression();
            if (comparefunctionArg == null)
                return null;
            PopByte(); //EndFuncParms
            StartPositions.Pop();
            return new DynArraySort(arr, comparefunctionArg);
        }
    }
}
