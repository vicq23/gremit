﻿using System;
using System.Collections.Generic;
using System.Diagnostics.SymbolStore;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using GrEmit.InstructionComments;
using GrEmit.InstructionParameters;

namespace GrEmit
{
    // ReSharper disable InconsistentNaming
    public class GroboIL
    {
        public GroboIL(DynamicMethod method, bool analyzeStack = true)
        {
            this.analyzeStack = analyzeStack;
            il = method.GetILGenerator();
            methodReturnType = method.ReturnType;
            methodParameterTypes = Formatter.GetParameterTypes(method);
        }

        public GroboIL(MethodBuilder method, bool analyzeStack = true)
        {
            this.analyzeStack = analyzeStack;
            il = method.GetILGenerator();
            methodReturnType = method.ReturnType;
            Type[] parameterTypes = Formatter.GetParameterTypes(method);
            methodParameterTypes = method.IsStatic ? parameterTypes : new[] {method.ReflectedType}.Concat(parameterTypes).ToArray();
        }

        public GroboIL(ConstructorBuilder constructor, bool analyzeStack = true)
        {
            this.analyzeStack = analyzeStack;
            il = constructor.GetILGenerator();
            methodReturnType = typeof(void);
            methodParameterTypes = new[] {constructor.ReflectedType}.Concat(Formatter.GetParameterTypes(constructor)).ToArray();
        }

        public string GetILCode()
        {
            return ilCode.ToString();
        }

        /// <summary>
        /// Declares a local variable of the specified type, optionally pinning the object referred to by the variable.
        /// </summary>
        /// <param name="localType">A <see cref="System.Type">Type</see> object that represents the type of the local variable.</param>
        /// <param name="name">Name of the local being declared.</param>
        /// <param name="pinned">true to pin the object in memory; otherwise, false.</param>
        /// <returns>A <see cref="Local">Local</see> object that represents the local variable.</returns>
        public Local DeclareLocal(Type localType, string name, bool pinned = false)
        {
            return new Local(il.DeclareLocal(localType, pinned), (string.IsNullOrEmpty(name) ? "local" : name) + "_" + localId++);
        }

        /// <summary>
        /// Declares a local variable of the specified type, optionally pinning the object referred to by the variable.
        /// </summary>
        /// <param name="localType">A <see cref="System.Type">Type</see> object that represents the type of the local variable.</param>
        /// <param name="pinned">true to pin the object in memory; otherwise, false.</param>
        /// <returns>A <see cref="Local">Local</see> object that represents the local variable.</returns>
        public Local DeclareLocal(Type localType, bool pinned = false)
        {
            return new Local(il.DeclareLocal(localType, pinned), "local_" + localId++);
        }

        /// <summary>
        /// Declares a new label.
        /// </summary>
        /// <param name="name">Name of label.</param>
        /// <returns>A <see cref="Label">Label</see> object that can be used as a token for branching.</returns>
        public Label DefineLabel(string name)
        {
            return new Label(il.DefineLabel(), name + "_" + labelId++);
        }

        /// <summary>
        /// Marks the Common intermediate language (CIL) stream's current position with the given label.
        /// </summary>
        /// <param name="label">The <see cref="Label">Label</see> object to mark the CIL stream's current position with.</param>
        public void MarkLabel(Label label)
        {
            if(analyzeStack)
                MutateStack(default(OpCode), new LabelILInstructionParameter(label));
            ilCode.MarkLabel(label, GetComment());
            il.MarkLabel(label);
        }

        /// <summary>
        /// Emits the Common intermediate language (CIL) to call System.Console.WriteLine with a string.
        /// </summary>
        /// <param name="str">The string to be printed. </param>
        public void EmitWriteLine(string str)
        {
            il.EmitWriteLine(str);
        }

        /// <summary>
        /// Marks a sequence point in the Common intermediate language (CIL) stream.
        /// </summary>
        /// <param name="document">The document for which the sequence point is being defined.</param>
        /// <param name="startLine">The line where the sequence point begins.</param>
        /// <param name="startColumn">The column in the line where the sequence point begins.</param>
        /// <param name="endLine">The line where the sequence point ends.</param>
        /// <param name="endColumn">The column in the line where the sequence point ends.</param>
        public void MarkSequencePoint(ISymbolDocumentWriter document, int startLine, int startColumn, int endLine, int endColumn)
        {
            il.MarkSequencePoint(document, startLine, startColumn, endLine, endColumn);
        }

        /// <summary>
        /// Begins an exception block for a non-filtered exception.
        /// </summary>
        /// <returns>The <see cref="Label">Label</see> object for the end of the block. This will leave you in the correct place to execute finally blocks or to finish the try.</returns>
        public Label BeginExceptionBlock()
        {
            ilCode.BeginExceptionBlock(GetComment());
            return new Label(il.BeginExceptionBlock(), "TRY");
        }

        /// <summary>
        /// Begins a catch block.
        /// </summary>
        /// <param name="exceptionType">The <see cref="Type">Type</see> object that represents the exception. </param>
        public void BeginCatchBlock(Type exceptionType)
        {
            if(exceptionType != null)
            {
                if(analyzeStack)
                    stack = new Stack<Type>(new[] {exceptionType});
                ilCode.BeginCatchBlock(new TypeILInstructionParameter(exceptionType), GetComment());
            }
            il.BeginCatchBlock(exceptionType);
        }

        /// <summary>
        /// Begins an exception block for a filtered exception.
        /// </summary>
        public void BeginExceptFilterBlock()
        {
            if(analyzeStack)
                stack = new Stack<Type>(new[] {typeof(Exception)});
            ilCode.BeginExceptFilterBlock(GetComment());
            il.BeginExceptFilterBlock();
        }

        /// <summary>
        /// Begins an exception fault block in the Common intermediate language (CIL) stream.
        /// </summary>
        public void BeginFaultBlock()
        {
            if(analyzeStack)
                stack = new Stack<Type>();
            ilCode.BeginFaultBlock(GetComment());
            il.BeginFaultBlock();
        }

        /// <summary>
        /// Begins a finally block in the Common intermediate language (CIL) instruction stream.
        /// </summary>
        public void BeginFinallyBlock()
        {
            if(analyzeStack)
                stack = new Stack<Type>();
            ilCode.BeginFinallyBlock(GetComment());
            il.BeginFinallyBlock();
        }

        /// <summary>
        /// Ends an exception block.
        /// </summary>
        public void EndExceptionBlock()
        {
            ilCode.EndExceptionBlock(GetComment());
            il.EndExceptionBlock();
        }

        /// <summary>
        /// Fills space if opcodes are patched. No meaningful operation is performed although a processing cycle can be consumed.
        /// </summary>
        public void Nop()
        {
            Emit(OpCodes.Nop);
        }

        /// <summary>
        /// Throws the exception object currently on the evaluation stack.
        /// </summary>
        public void Throw()
        {
            Emit(OpCodes.Throw);
        }

        /// <summary>
        /// Implements a jump table.
        /// </summary>
        /// <param name="labels">The array of <see cref="Label">Label</see> object to jump to.</param>
        public void Switch(params Label[] labels)
        {
            if(labels == null)
                throw new ArgumentNullException("labels");
            if(labels.Length == 0)
                throw new ArgumentException("At least one label must be specified", "labels");
            Emit(OpCodes.Switch, labels);
        }

        /// <summary>
        /// Returns from the current method, pushing a return value (if present) from the callee's evaluation stack onto the caller's evaluation stack.
        /// </summary>
        public void Ret()
        {
            Emit(OpCodes.Ret);
            stack = null;
        }

        /// <summary>
        /// Exits a protected region of code, unconditionally transferring control to a specific target instruction.
        /// </summary>
        /// <param name="label">The <see cref="Label">Label</see> object to jump to.</param>
        public void Leave(Label label)
        {
            if(label == null)
                throw new ArgumentNullException("label");
            Emit(OpCodes.Leave, label);
            stack = null;
        }

        /// <summary>
        /// Unconditionally transfers control to a target instruction.
        /// </summary>
        /// <param name="label">The <see cref="Label">Label</see> object to jump to.</param>
        public void Br(Label label)
        {
            if(label == null)
                throw new ArgumentNullException("label");
            Emit(OpCodes.Br, label);
            stack = null;
        }

        /// <summary>
        /// Transfers control to a target instruction if value is false, a null reference, or zero.
        /// </summary>
        /// <param name="label">The <see cref="Label">Label</see> object to jump to.</param>
        public void Brfalse(Label label)
        {
            if(label == null)
                throw new ArgumentNullException("label");
            Emit(OpCodes.Brfalse, label);
        }

        /// <summary>
        /// Transfers control to a target instruction if value is true, not null, or non-zero.
        /// </summary>
        /// <param name="label">The <see cref="Label">Label</see> object to jump to.</param>
        public void Brtrue(Label label)
        {
            if(label == null)
                throw new ArgumentNullException("label");
            Emit(OpCodes.Brtrue, label);
        }

        /// <summary>
        /// Transfers control to a target instruction if the first value is less than or equal to the second value.
        /// </summary>
        /// <param name="type">
        /// A <see cref="Type">Type</see> object representing the type of values being compared.
        /// <para></para>
        /// Depending on whether the <paramref name="type"/> is signed or unsigned either <see cref="OpCodes.Ble">Ble</see> or <see cref="OpCodes.Ble_Un">Ble_Un</see> instruction will be emitted.
        /// </param>
        /// <param name="label">The <see cref="Label">Label</see> object to jump to.</param>
        public void Ble(Type type, Label label)
        {
            if(label == null)
                throw new ArgumentNullException("label");
            Emit(Unsigned(type) ? OpCodes.Ble_Un : OpCodes.Ble, label);
        }

        /// <summary>
        /// Transfers control to a target instruction if the first value is greater than or equal to the second value.
        /// </summary>
        /// <param name="type">
        /// A <see cref="Type">Type</see> object representing the type of values being compared.
        /// <para></para>
        /// Depending on whether the <paramref name="type"/> is signed or unsigned either <see cref="OpCodes.Bge">Bge</see> or <see cref="OpCodes.Bge_Un">Bge_Un</see> instruction will be emitted.
        /// </param>
        /// <param name="label">The <see cref="Label">Label</see> object to jump to.</param>
        public void Bge(Type type, Label label)
        {
            if(label == null)
                throw new ArgumentNullException("label");
            Emit(Unsigned(type) ? OpCodes.Bge_Un : OpCodes.Bge, label);
        }

        /// <summary>
        /// Transfers control to a target instruction if the first value is less than the second value.
        /// </summary>
        /// <param name="type">
        /// A <see cref="Type">Type</see> object representing the type of values being compared.
        /// <para></para>
        /// Depending on whether the <paramref name="type"/> is signed or unsigned either <see cref="OpCodes.Blt">Blt</see> or <see cref="OpCodes.Blt_Un">Blt_Un</see> instruction will be emitted.
        /// </param>
        /// <param name="label">The <see cref="Label">Label</see> object to jump to.</param>
        public void Blt(Type type, Label label)
        {
            if(label == null)
                throw new ArgumentNullException("label");
            Emit(Unsigned(type) ? OpCodes.Blt_Un : OpCodes.Blt, label);
        }

        /// <summary>
        /// Transfers control to a target instruction if the first value is greater than the second value.
        /// </summary>
        /// <param name="type">
        /// A <see cref="Type">Type</see> object representing the type of values being compared.
        /// <para></para>
        /// Depending on whether the <paramref name="type"/> is signed or unsigned either <see cref="OpCodes.Bgt">Bgt</see> or <see cref="OpCodes.Bgt_Un">Bgt_Un</see> instruction will be emitted.
        /// </param>
        /// <param name="label">The <see cref="Label">Label</see> object to jump to.</param>
        public void Bgt(Type type, Label label)
        {
            if(label == null)
                throw new ArgumentNullException("label");
            Emit(Unsigned(type) ? OpCodes.Bgt_Un : OpCodes.Bgt, label);
        }

        /// <summary>
        /// Transfers control to a target instruction when two unsigned integer values or unordered float values are not equal.
        /// </summary>
        /// <param name="label">The <see cref="Label">Label</see> object to jump to.</param>
        public void Bne(Label label)
        {
            if(label == null)
                throw new ArgumentNullException("label");
            Emit(OpCodes.Bne_Un, label);
        }

        /// <summary>
        /// Transfers control to a target instruction if two values are equal.
        /// </summary>
        /// <param name="label">The <see cref="Label">Label</see> object to jump to.</param>
        public void Beq(Label label)
        {
            if(label == null)
                throw new ArgumentNullException("label");
            Emit(OpCodes.Beq, label);
        }

        /// <summary>
        /// Removes the value currently on top of the evaluation stack.
        /// </summary>
        public void Pop()
        {
            Emit(OpCodes.Pop);
        }

        /// <summary>
        /// Copies the current topmost value on the evaluation stack, and then pushes the copy onto the evaluation stack.
        /// </summary>
        public void Dup()
        {
            Emit(OpCodes.Dup);
        }

        /// <summary>
        /// Loads the address of the local variable at a specific index onto the evaluation stack.
        /// </summary>
        /// <param name="local">The <see cref="Local">Local</see> object whose address needs to be loaded onto the evaluation stack.</param>
        public void Ldloca(Local local)
        {
            Emit(OpCodes.Ldloca, local);
        }

        /// <summary>
        /// Loads the local variable at a specific index onto the evaluation stack.
        /// </summary>
        /// <param name="local">The <see cref="Local">Local</see> object which needs to be loaded onto the evaluation stack.</param>
        public void Ldloc(Local local)
        {
            Emit(OpCodes.Ldloc, local);
        }

        /// <summary>
        /// Pops the current value from the top of the evaluation stack and stores it in a the local variable list at a specified index.
        /// </summary>
        /// <param name="local">The <see cref="Local">Local</see> object in which the value must be stored.</param>
        public void Stloc(Local local)
        {
            Emit(OpCodes.Stloc, local);
        }

        /// <summary>
        /// Pushes a null reference (type O) onto the evaluation stack.
        /// </summary>
        /// <param name="type">
        /// The <see cref="Type">Type</see> of object being pushed onto the evaluation stack.
        /// <para></para>
        /// Needed only for increasing readability of the IL code being generated.
        /// </param>
        public void Ldnull(Type type)
        {
            Emit(OpCodes.Ldnull, new TypeILInstructionParameter(type));
        }

        /// <summary>
        /// Initializes each field of the value type at a specified address to a null reference or a 0 of the appropriate primitive type.
        /// </summary>
        /// <param name="type">The <see cref="Type">Type</see> of object being initialized. Must be a value type.</param>
        public void Initobj(Type type)
        {
            if(type == null)
                throw new ArgumentNullException("type");
            if(!type.IsValueType)
                throw new ArgumentException("A value type expected", "type");
            Emit(OpCodes.Initobj, type);
        }

        /// <summary>
        /// Loads an argument (referenced by a specified index value) onto the evaluation stack.
        /// </summary>
        /// <param name="index">
        /// Index of the argument being pushed.
        /// <para></para>
        /// Depending on that index emits on of the following instructions:
        /// <para></para>
        /// <see cref="OpCodes.Ldarg_0">Ldarg_0</see>, <see cref="OpCodes.Ldarg_1">Ldarg_1</see>, <see cref="OpCodes.Ldarg_2">Ldarg_2</see>,
        /// <see cref="OpCodes.Ldarg_3">Ldarg_3</see>, <see cref="OpCodes.Ldarg_S">Ldarg_S</see>, <see cref="OpCodes.Ldarg">Ldarg</see>
        /// </param>
        public void Ldarg(int index)
        {
            switch(index)
            {
            case 0:
                Emit(OpCodes.Ldarg_0);
                break;
            case 1:
                Emit(OpCodes.Ldarg_1);
                break;
            case 2:
                Emit(OpCodes.Ldarg_2);
                break;
            case 3:
                Emit(OpCodes.Ldarg_3);
                break;
            default:
                if(index < 256)
                    Emit(OpCodes.Ldarg_S, (byte)index);
                else
                    Emit(OpCodes.Ldarg, index);
                break;
            }
        }

        /// <summary>
        /// Stores the value on top of the evaluation stack in the argument slot at a specified index.
        /// </summary>
        /// <param name="index">
        /// Index of the argument to store the value in.
        /// <para></para>
        /// Depending on that index emits either <see cref="OpCodes.Starg_S">Starg_S</see> or <see cref="OpCodes.Starg">Starg</see> instruction.
        /// </param>
        public void Starg(int index)
        {
            if(index < 256)
                Emit(OpCodes.Starg_S, (byte)index);
            else
                Emit(OpCodes.Starg, index);
        }

        /// <summary>
        /// Load an argument address onto the evaluation stack.
        /// </summary>
        /// <param name="index">
        /// Index of the argument to load address of.
        /// <para></para>
        /// Depending on that index emits either <see cref="OpCodes.Ldarga_S">Ldarga_S</see> or <see cref="OpCodes.Ldarga">Ldarga</see> instruction.
        /// </param>
        public void Ldarga(int index)
        {
            if(index < 256)
                Emit(OpCodes.Ldarga_S, (byte)index);
            else
                Emit(OpCodes.Ldarga, index);
        }

        /// <summary>
        /// Pushes a supplied value of type int32 onto the evaluation stack as an int32.
        /// </summary>
        /// <param name="value">
        /// The value to push.
        /// <para></para>
        /// Depending on the value emits one of the following instructions:
        /// <para></para>
        /// <see cref="OpCodes.Ldc_I4_0">Ldc_I4_0</see>, <see cref="OpCodes.Ldc_I4_1">Ldc_I4_1</see>, <see cref="OpCodes.Ldc_I4_2">Ldc_I4_2</see>, <see cref="OpCodes.Ldc_I4_3">Ldc_I4_3</see>,
        /// <see cref="OpCodes.Ldc_I4_4">Ldc_I4_4</see>, <see cref="OpCodes.Ldc_I4_5">Ldc_I4_5</see>, <see cref="OpCodes.Ldc_I4_6">Ldc_I4_6</see>, <see cref="OpCodes.Ldc_I4_7">Ldc_I4_7</see>,
        /// <see cref="OpCodes.Ldc_I4_8">Ldc_I4_8</see>, <see cref="OpCodes.Ldc_I4_M1">Ldc_I4_M1</see>, <see cref="OpCodes.Ldc_I4_S">Ldc_I4_S</see>, <see cref="OpCodes.Ldc_I4">Ldc_I4</see>
        /// </param>
        public void Ldc_I4(int value)
        {
            switch(value)
            {
            case 0:
                Emit(OpCodes.Ldc_I4_0);
                break;
            case 1:
                Emit(OpCodes.Ldc_I4_1);
                break;
            case 2:
                Emit(OpCodes.Ldc_I4_2);
                break;
            case 3:
                Emit(OpCodes.Ldc_I4_3);
                break;
            case 4:
                Emit(OpCodes.Ldc_I4_4);
                break;
            case 5:
                Emit(OpCodes.Ldc_I4_5);
                break;
            case 6:
                Emit(OpCodes.Ldc_I4_6);
                break;
            case 7:
                Emit(OpCodes.Ldc_I4_7);
                break;
            case 8:
                Emit(OpCodes.Ldc_I4_8);
                break;
            case -1:
                Emit(OpCodes.Ldc_I4_M1);
                break;
            default:
                if(value < 128 && value >= -128)
                    Emit(OpCodes.Ldc_I4_S, (sbyte)value);
                else
                    Emit(OpCodes.Ldc_I4, value);
                break;
            }
        }

        /// <summary>
        /// Pushes a supplied value of type int64 onto the evaluation stack as an int64.
        /// </summary>
        /// <param name="value">The value to push.</param>
        public void Ldc_I8(long value)
        {
            Emit(OpCodes.Ldc_I8, value);
        }

        /// <summary>
        /// Pushes a supplied value of type float32 onto the evaluation stack as type F (float).
        /// </summary>
        /// <param name="value">The value to push.</param>
        public void Ldc_R4(float value)
        {
            Emit(OpCodes.Ldc_R4, value);
        }

        /// <summary>
        /// Pushes a supplied value of type float64 onto the evaluation stack as type F (float).
        /// </summary>
        /// <param name="value">The value to push.</param>
        public void Ldc_R8(double value)
        {
            Emit(OpCodes.Ldc_R8, value);
        }

        /// <summary>
        /// Pushes a supplied value of type IntPtr onto the evaluation stack as type native int
        /// </summary>
        /// <param name="value">The value to push.</param>
        public void Ldc_IntPtr(IntPtr value)
        {
            if(IntPtr.Size == 4)
                Ldc_I4(value.ToInt32());
            else
                Ldc_I8(value.ToInt64());
        }

        /// <summary>
        /// Pushes the number of elements of a zero-based, one-dimensional array onto the evaluation stack.
        /// </summary>
        public void Ldlen()
        {
            Emit(OpCodes.Ldlen);
        }

        /// <summary>
        /// Pushes an unmanaged pointer (type native int) to the native code implementing a specific method onto the evaluation stack.
        /// </summary>
        /// <param name="method">The method to load address of.</param>
        public void Ldftn(MethodInfo method)
        {
            if(method == null)
                throw new ArgumentNullException("method");
            Emit(OpCodes.Ldftn, method);
        }

        /// <summary>
        /// Replaces the value of a field with a value from the evaluation stack.
        /// </summary>
        /// <param name="field">
        /// The field to store value in.
        /// <para></para>
        /// Depending on whether the field is static or not emits either <see cref="OpCodes.Stsfld">Stsfld</see> or <see cref="OpCodes.Stfld">Stfld</see> respectively.
        /// </param>
        public void Stfld(FieldInfo field)
        {
            if(field == null)
                throw new ArgumentNullException("field");
            Emit(field.IsStatic ? OpCodes.Stsfld : OpCodes.Stfld, field);
        }

        /// <summary>
        /// Pushes the value of a field onto the evaluation stack.
        /// </summary>
        /// <param name="field">
        /// The field to load value of.
        /// <para></para>
        /// Depending on whether the field is static or not emits either <see cref="OpCodes.Ldsfld">Ldsfld</see> or <see cref="OpCodes.Ldfld">Ldfld</see> respectively.
        /// </param>
        public void Ldfld(FieldInfo field)
        {
            if(field == null)
                throw new ArgumentNullException("field");
            Emit(field.IsStatic ? OpCodes.Ldsfld : OpCodes.Ldfld, field);
        }

        /// <summary>
        /// Pushes the address of a field onto the evaluation stack.
        /// </summary>
        /// <param name="field">
        /// The field to load address of.
        /// <para></para>
        /// Depending on whether the field is static or not emits either <see cref="OpCodes.Ldsflda">Ldsflda</see> or <see cref="OpCodes.Ldflda">Ldflda</see> respectively.
        /// </param>
        public void Ldflda(FieldInfo field)
        {
            if(field == null)
                throw new ArgumentNullException("field");
            Emit(field.IsStatic ? OpCodes.Ldsflda : OpCodes.Ldflda, field);
        }

        /// <summary>
        /// Loads the address of the array element at a specified array index onto the top of the evaluation stack as type &amp; (managed pointer).
        /// </summary>
        /// <param name="elementType">The element type of the array.</param>
        public void Ldelema(Type elementType)
        {
            if(elementType == null)
                throw new ArgumentNullException("elementType");
            Emit(OpCodes.Ldelema, elementType);
        }

        /// <summary>
        /// Loads the element at a specified array index onto the top of the evaluation stack.
        /// </summary>
        /// <param name="elementType">
        /// The element type of the array.
        /// <para></para>
        /// Depending on that type emits one of the following instructions:
        /// <para></para>
        /// <see cref="OpCodes.Ldelem_Ref">Ldelem_Ref</see>, <see cref="OpCodes.Ldelem_I">Ldelem_I</see>, <see cref="OpCodes.Ldelem_I1">Ldelem_I1</see>, <see cref="OpCodes.Ldelem_I2">Ldelem_I2</see>, 
        /// <see cref="OpCodes.Ldelem_I4">Ldelem_I4</see>, <see cref="OpCodes.Ldelem_I8">Ldelem_I8</see>, <see cref="OpCodes.Ldelem_U1">Ldelem_U1</see>, <see cref="OpCodes.Ldelem_U2">Ldelem_U2</see>, 
        /// <see cref="OpCodes.Ldelem_U4">Ldelem_U4</see>, <see cref="OpCodes.Ldelem_R4">Ldelem_R4</see>, <see cref="OpCodes.Ldelem_R8">Ldelem_R8</see>
        /// <para></para>
        /// If the element type is a user-defined value type emits <see cref="OpCodes.Ldelema">Ldelema</see> &amp; <see cref="OpCodes.Ldobj">Ldobj</see> instructions.
        /// </param>
        public void Ldelem(Type elementType)
        {
            if(elementType == null)
                throw new ArgumentNullException("elementType");
            if(IsStruct(elementType))
            {
                // struct
                Ldelema(elementType);
                Ldobj(elementType);
                return;
            }
            var parameter = new TypeILInstructionParameter(elementType);
            if(!elementType.IsValueType) // class
                Emit(OpCodes.Ldelem_Ref, parameter);
            else if(elementType == typeof(IntPtr) || elementType == typeof(UIntPtr))
                Emit(OpCodes.Ldelem_I, parameter);
            else
            {
                // Primitive
                switch(Type.GetTypeCode(elementType))
                {
                case TypeCode.Boolean:
                case TypeCode.SByte:
                    Emit(OpCodes.Ldelem_I1, parameter);
                    break;
                case TypeCode.Byte:
                    Emit(OpCodes.Ldelem_U1, parameter);
                    break;
                case TypeCode.Int16:
                    Emit(OpCodes.Ldelem_I2, parameter);
                    break;
                case TypeCode.Int32:
                    Emit(OpCodes.Ldelem_I4, parameter);
                    break;
                case TypeCode.Int64:
                case TypeCode.UInt64:
                    Emit(OpCodes.Ldelem_I8, parameter);
                    break;
                case TypeCode.Char:
                case TypeCode.UInt16:
                    Emit(OpCodes.Ldelem_U2, parameter);
                    break;
                case TypeCode.UInt32:
                    Emit(OpCodes.Ldelem_U4, parameter);
                    break;
                case TypeCode.Single:
                    Emit(OpCodes.Ldelem_R4, parameter);
                    break;
                case TypeCode.Double:
                    Emit(OpCodes.Ldelem_R8, parameter);
                    break;
                default:
                    throw new NotSupportedException("Type '" + elementType.Name + "' is not supported");
                }
            }
        }

        /// <summary>
        /// Replaces the array element at a given index with the value on the evaluation stack.
        /// </summary>
        /// <param name="elementType">
        /// The element type of the array.
        /// <para></para>
        /// Depending on that type emits one of the following instructions:
        /// <para></para>
        /// <see cref="OpCodes.Stelem_Ref">Stelem_Ref</see>, <see cref="OpCodes.Stelem_I">Stelem_I</see>, <see cref="OpCodes.Stelem_I1">Stelem_I1</see>, <see cref="OpCodes.Stelem_I2">Stelem_I2</see>, 
        /// <see cref="OpCodes.Stelem_I4">Stelem_I4</see>, <see cref="OpCodes.Stelem_I8">Stelem_I8</see>, <see cref="OpCodes.Stelem_R4">Stelem_R4</see>, <see cref="OpCodes.Stelem_R8">Stelem_R8</see>
        /// <para></para>
        /// DOES NOT WORK if the element type is a user-defined value type. In such a case emit <see cref="OpCodes.Ldelema">Ldelema</see> &amp; <see cref="OpCodes.Stobj">Stobj</see> instructions.
        /// </param>
        public void Stelem(Type elementType)
        {
            if(elementType == null)
                throw new ArgumentNullException("elementType");
            if(IsStruct(elementType))
                throw new InvalidOperationException("To store an item to an array of structs use Ldelema & Stobj instructions");

            var parameter = new TypeILInstructionParameter(elementType);
            if(!elementType.IsValueType) // class
                Emit(OpCodes.Stelem_Ref, parameter);
            else if(elementType == typeof(IntPtr) || elementType == typeof(UIntPtr))
                Emit(OpCodes.Stelem_I, parameter);
            else
            {
                // Primitive
                switch(Type.GetTypeCode(elementType))
                {
                case TypeCode.Boolean:
                case TypeCode.SByte:
                case TypeCode.Byte:
                    Emit(OpCodes.Stelem_I1, parameter);
                    break;
                case TypeCode.Char:
                case TypeCode.UInt16:
                case TypeCode.Int16:
                    Emit(OpCodes.Stelem_I2, parameter);
                    break;
                case TypeCode.Int32:
                case TypeCode.UInt32:
                    Emit(OpCodes.Stelem_I4, parameter);
                    break;
                case TypeCode.Int64:
                case TypeCode.UInt64:
                    Emit(OpCodes.Stelem_I8, parameter);
                    break;
                case TypeCode.Single:
                    Emit(OpCodes.Stelem_R4, parameter);
                    break;
                case TypeCode.Double:
                    Emit(OpCodes.Stelem_R8, parameter);
                    break;
                default:
                    throw new NotSupportedException("Type '" + elementType.Name + "' is not supported");
                }
            }
        }

        /// <summary>
        /// Stores a value of a specified type at a specified address.
        /// </summary>
        /// <param name="type">
        /// The type of a value being stored.
        /// <para></para>
        /// Depending on that type emits one of the following instructions:
        /// <para></para>
        /// <see cref="OpCodes.Stind_Ref">Stind_Ref</see>, <see cref="OpCodes.Stind_I1">Stind_I1</see>, <see cref="OpCodes.Stind_I2">Stind_I2</see>, <see cref="OpCodes.Stind_I4">Stind_I4</see>, 
        /// <see cref="OpCodes.Stind_I8">Stind_I8</see>, <see cref="OpCodes.Stind_R4">Stind_R4</see>, <see cref="OpCodes.Stind_R8">Stind_R8</see>
        /// <para></para>
        /// If the value is of a user-defined value type emits <see cref="OpCodes.Stobj">Stobj</see> instruction.
        /// </param>
        public void Stind(Type type)
        {
            if(type == null)
                throw new ArgumentNullException("type");
            if(IsStruct(type))
            {
                Stobj(type);
                return;
            }

            var parameter = new TypeILInstructionParameter(type);
            if(!type.IsValueType) // class
                Emit(OpCodes.Stind_Ref, parameter);
            else
            {
                // Primitive
                switch(Type.GetTypeCode(type))
                {
                case TypeCode.Boolean:
                case TypeCode.SByte:
                case TypeCode.Byte:
                    Emit(OpCodes.Stind_I1, parameter);
                    break;
                case TypeCode.Int16:
                case TypeCode.Char:
                case TypeCode.UInt16:
                    Emit(OpCodes.Stind_I2, parameter);
                    break;
                case TypeCode.Int32:
                case TypeCode.UInt32:
                    Emit(OpCodes.Stind_I4, parameter);
                    break;
                case TypeCode.Int64:
                case TypeCode.UInt64:
                    Emit(OpCodes.Stind_I8, parameter);
                    break;
                case TypeCode.Single:
                    Emit(OpCodes.Stind_R4, parameter);
                    break;
                case TypeCode.Double:
                    Emit(OpCodes.Stind_R8, parameter);
                    break;
                default:
                    throw new NotSupportedException("Type '" + type.Name + "' is not supported");
                }
            }
        }

        /// <summary>
        /// Loads a value of a specifed type onto the evaluation stack indirectly.
        /// </summary>
        /// <param name="type">
        /// The <see cref="Type">Type</see> of a value being loaded.
        /// <para></para>
        /// Depending on that type emits one of the following instructions:
        /// <para></para>
        /// <see cref="OpCodes.Ldind_Ref">Ldind_Ref</see>, <see cref="OpCodes.Ldind_I1">Ldind_I1</see>, <see cref="OpCodes.Ldind_I2">Ldind_I2</see>, <see cref="OpCodes.Ldind_I4">Ldind_I4</see>, 
        /// <see cref="OpCodes.Ldind_I8">Ldind_I8</see>, <see cref="OpCodes.Ldind_U1">Ldind_U1</see>, <see cref="OpCodes.Ldind_U2">Ldind_U2</see>, <see cref="OpCodes.Ldind_U4">Ldind_U4</see>,
        /// <see cref="OpCodes.Ldind_R4">Ldind_R4</see>, <see cref="OpCodes.Ldind_R8">Ldind_R8</see>
        /// <para></para>
        /// If the value is of a user-defined value type emits <see cref="OpCodes.Ldobj">Ldobj</see> instruction.
        /// </param>
        public void Ldind(Type type)
        {
            if(type == null)
                throw new ArgumentNullException("type");
            if(IsStruct(type))
            {
                Ldobj(type);
                return;
            }

            var parameter = new TypeILInstructionParameter(type);
            if(!type.IsValueType) // class
                Emit(OpCodes.Ldind_Ref, parameter);
            else
            {
                switch(Type.GetTypeCode(type))
                {
                case TypeCode.SByte:
                    Emit(OpCodes.Ldind_I1, parameter);
                    break;
                case TypeCode.Byte:
                case TypeCode.Boolean:
                    Emit(OpCodes.Ldind_U1, parameter);
                    break;
                case TypeCode.Int16:
                    Emit(OpCodes.Ldind_I2, parameter);
                    break;
                case TypeCode.Char:
                case TypeCode.UInt16:
                    Emit(OpCodes.Ldind_U2, parameter);
                    break;
                case TypeCode.Int32:
                    Emit(OpCodes.Ldind_I4, parameter);
                    break;
                case TypeCode.UInt32:
                    Emit(OpCodes.Ldind_U4, parameter);
                    break;
                case TypeCode.Int64:
                case TypeCode.UInt64:
                    Emit(OpCodes.Ldind_I8, parameter);
                    break;
                case TypeCode.Single:
                    Emit(OpCodes.Ldind_R4, parameter);
                    break;
                case TypeCode.Double:
                    Emit(OpCodes.Ldind_R8, parameter);
                    break;
                default:
                    throw new NotSupportedException("Type '" + type.Name + "' is not supported");
                }
            }
        }

        /// <summary>
        /// Copies a specified number bytes from a source address to a destination address.
        /// </summary>
        public void Cpblk()
        {
            Emit(OpCodes.Cpblk);
        }

        /// <summary>
        /// Indicates that an address currently atop the evaluation stack might not be aligned to the natural size of the immediately following ldind, stind, ldfld, stfld, ldobj, stobj, initblk, or cpblk instruction.
        /// </summary>
        /// <param name="value">The value of alignment. Must be 1, 2 or 4.</param>
        public void Unaligned(long value)
        {
            il.Emit(OpCodes.Unaligned, value);
        }

        /// <summary>
        /// Converts a metadata token of a specified type to its runtime representation, pushing it onto the evaluation stack.
        /// </summary>
        /// <param name="type">The <see cref="Type">Type</see> object metadata token of which is being pushed onto the evaluation stack.</param>
        public void Ldtoken(Type type)
        {
            if(type == null)
                throw new ArgumentNullException("type");
            Emit(OpCodes.Ldtoken, type);
        }

        /// <summary>
        /// Converts a metadata token of a specified method to its runtime representation, pushing it onto the evaluation stack.
        /// </summary>
        /// <param name="method">The <see cref="MethodInfo">MethodInfo</see> object metadata token of which is being pushed onto the evaluation stack.</param>
        public void Ldtoken(MethodInfo method)
        {
            if(method == null)
                throw new ArgumentNullException("method");
            Emit(OpCodes.Ldtoken, method);
        }

        /// <summary>
        /// Converts a metadata token of a specified field to its runtime representation, pushing it onto the evaluation stack.
        /// </summary>
        /// <param name="field">The <see cref="FieldInfo">FieldInfo</see> object metadata token of which is being pushed onto the evaluation stack.</param>
        public void Ldtoken(FieldInfo field)
        {
            if(field == null)
                throw new ArgumentNullException("field");
            Emit(OpCodes.Ldtoken, field);
        }

        /// <summary>
        /// Attempts to cast an object passed by reference to the specified class.
        /// </summary>
        /// <param name="type">The <see cref="Type">Type</see> to cast an object to.</param>
        public void Castclass(Type type)
        {
            if(type == null)
                throw new ArgumentNullException("type");
            if(type.IsValueType)
                throw new ArgumentException("A reference type expected", "type");
            Emit(OpCodes.Castclass, type);
        }

        /// <summary>
        /// Tests whether an object reference (type O) is an instance of a particular class.
        /// </summary>
        /// <param name="type">The <see cref="Type">Type</see> to test.</param>
        public void Isinst(Type type)
        {
            if(type == null)
                throw new ArgumentNullException("type");
            Emit(OpCodes.Isinst, type);
        }

        /// <summary>
        /// Converts the boxed representation of a type specified in the instruction to its unboxed form.
        /// </summary>
        /// <param name="type">The <see cref="Type">Type</see> of boxed object. Must be a value type.</param>
        public void Unbox_Any(Type type)
        {
            if(type == null)
                throw new ArgumentNullException("type");
            if(!type.IsValueType)
                throw new ArgumentException("A value type expected", "type");
            Emit(OpCodes.Unbox_Any, type);
        }

        /// <summary>
        /// Converts a value type to an object reference (type O).
        /// </summary>
        /// <param name="type">The <see cref="Type">Type</see> of object to box.</param>
        public void Box(Type type)
        {
            if(type == null)
                throw new ArgumentNullException("type");
            if(!type.IsValueType)
                throw new ArgumentException("A value type expected", "type");
            Emit(OpCodes.Box, type);
        }

        /// <summary>
        /// Emits the Common Intermediate Language (CIL) necessary to call System.Console.WriteLine with the given local variable.
        /// </summary>
        /// <param name="local">The <see cref="Local">Local</see> to write.</param>
        public void WriteLine(Local local)
        {
            il.EmitWriteLine(local);
        }

        /// <summary>
        /// Emits the Common Intermediate Language (CIL) to call System.Console.WriteLine with a string.
        /// </summary>
        /// <param name="str">The <see cref="String">String</see> to write.</param>
        public void WriteLine(string str)
        {
            il.EmitWriteLine(str);
        }

        /// <summary>
        /// Copies a value of a specified type from the evaluation stack into a supplied memory address.
        /// </summary>
        /// <param name="type">The <see cref="Type">Type</see> of object to be stored.</param>
        public void Stobj(Type type)
        {
            if(type == null)
                throw new ArgumentNullException("type");
            if(!type.IsValueType)
                throw new ArgumentException("A value type expected", "type");
            Emit(OpCodes.Stobj, type);
        }

        /// <summary>
        /// Copies the value type object pointed to by an address to the top of the evaluation stack.
        /// </summary>
        /// <param name="type">The <see cref="Type">Type</see> of object to be loaded.</param>
        public void Ldobj(Type type)
        {
            if(type == null)
                throw new ArgumentNullException("type");
            if(!type.IsValueType)
                throw new ArgumentException("A value type expected", "type");
            Emit(OpCodes.Ldobj, type);
        }

        /// <summary>
        /// Creates a new object or a new instance of a value type, pushing an object reference (type O) onto the evaluation stack.
        /// </summary>
        /// <param name="constructor">The <see cref="ConstructorInfo">Constructor</see> to be called.</param>
        public void Newobj(ConstructorInfo constructor)
        {
            if(constructor == null)
                throw new ArgumentNullException("constructor");
            Emit(OpCodes.Newobj, constructor);
        }

        /// <summary>
        /// Pushes an object reference to a new zero-based, one-dimensional array whose elements are of a specific type onto the evaluation stack.
        /// </summary>
        /// <param name="type">The <see cref="Type">Type</see> of elements.</param>
        public void Newarr(Type type)
        {
            if(type == null)
                throw new ArgumentNullException("type");
            Emit(OpCodes.Newarr, type);
        }

        /// <summary>
        /// Compares two values. If they are equal, the integer value 1 (int32) is pushed onto the evaluation stack; otherwise 0 (int32) is pushed onto the evaluation stack.
        /// </summary>
        public void Ceq()
        {
            Emit(OpCodes.Ceq);
        }

        /// <summary>
        /// Compares two values. If the first value is greater than the second, the integer value 1 (int32) is pushed onto the evaluation stack; otherwise 0 (int32) is pushed onto the evaluation stack.
        /// </summary>
        /// <param name="type">
        /// The <see cref="Type">Type</see> of the values being compared.
        /// <para></para>
        /// Emits either <see cref="OpCodes.Cgt">Cgt</see> or <see cref="OpCodes.Cgt_Un">Cgt_Un</see> instruction depending on whether the type is signed or not.
        /// </param>
        public void Cgt(Type type)
        {
            Emit(Unsigned(type) ? OpCodes.Cgt_Un : OpCodes.Cgt);
        }

        /// <summary>
        /// Compares two values. If the first value is less than the second, the integer value 1 (int32) is pushed onto the evaluation stack; otherwise 0 (int32) is pushed onto the evaluation stack.
        /// </summary>
        /// <param name="type">
        /// The <see cref="Type">Type</see> of the values being compared.
        /// <para></para>
        /// Emits either <see cref="OpCodes.Clt">Clt</see> or <see cref="OpCodes.Clt_Un">Clt_Un</see> instruction depending on whether the type is signed or not.
        /// </param>
        public void Clt(Type type)
        {
            Emit(Unsigned(type) ? OpCodes.Clt_Un : OpCodes.Clt);
        }

        /// <summary>
        /// Computes the bitwise AND of two values and pushes the result onto the evaluation stack.
        /// </summary>
        public void And()
        {
            Emit(OpCodes.And);
        }

        /// <summary>
        /// Compute the bitwise complement of the two integer values on top of the stack and pushes the result onto the evaluation stack.
        /// </summary>
        public void Or()
        {
            Emit(OpCodes.Or);
        }

        /// <summary>
        /// Computes the bitwise XOR of the top two values on the evaluation stack, pushing the result onto the evaluation stack.
        /// </summary>
        public void Xor()
        {
            Emit(OpCodes.Xor);
        }

        /// <summary>
        /// Adds two values and pushes the result onto the evaluation stack.
        /// </summary>
        public void Add()
        {
            Emit(OpCodes.Add);
        }

        /// <summary>
        /// Adds two integers, performs an overflow check, and pushes the result onto the evaluation stack.
        /// </summary>
        /// <param name="type">
        /// The <see cref="Type">Type</see> of the values being added.
        /// <para></para>
        /// Emits either <see cref="OpCodes.Add_Ovf">Add_Ovf</see> or <see cref="OpCodes.Add_Ovf_Un">Add_Ovf_Un</see> instruction depending on whether the type is signed or not.
        /// </param>
        public void Add_Ovf(Type type)
        {
            Emit(Unsigned(type) ? OpCodes.Add_Ovf_Un : OpCodes.Add_Ovf);
        }

        /// <summary>
        /// Subtracts one value from another and pushes the result onto the evaluation stack.
        /// </summary>
        public void Sub()
        {
            Emit(OpCodes.Sub);
        }

        /// <summary>
        /// Subtracts one integer value from another, performs an overflow check, and pushes the result onto the evaluation stack.
        /// </summary>
        /// <param name="type">
        /// The <see cref="Type">Type</see> of the values being subtracted.
        /// <para></para>
        /// Emits either <see cref="OpCodes.Sub_Ovf">Sub_Ovf</see> or <see cref="OpCodes.Sub_Ovf_Un">Sub_Ovf_Un</see> instruction depending on whether the type is signed or not.
        /// </param>
        public void Sub_Ovf(Type type)
        {
            Emit(Unsigned(type) ? OpCodes.Sub_Ovf_Un : OpCodes.Sub_Ovf);
        }

        /// <summary>
        /// Multiplies two values and pushes the result on the evaluation stack.
        /// </summary>
        public void Mul()
        {
            Emit(OpCodes.Mul);
        }

        /// <summary>
        /// Multiplies two integer values, performs an overflow check, and pushes the result onto the evaluation stack.
        /// </summary>
        /// <param name="type">
        /// The <see cref="Type">Type</see> of the values being multiplied.
        /// <para></para>
        /// Emits either <see cref="OpCodes.Mul_Ovf">Mul_Ovf</see> or <see cref="OpCodes.Mul_Ovf_Un">Mul_Ovf_Un</see> instruction depending on whether the type is signed or not.
        /// </param>
        public void Mul_Ovf(Type type)
        {
            Emit(Unsigned(type) ? OpCodes.Mul_Ovf_Un : OpCodes.Mul_Ovf);
        }

        /// <summary>
        /// Divides two values and pushes the result as a floating-point (type F) or quotient (type int32) onto the evaluation stack.
        /// </summary>
        /// <param name="type">
        /// The <see cref="Type">Type</see> of the values being divided.
        /// <para></para>
        /// Emits either <see cref="OpCodes.Div">Div</see> or <see cref="OpCodes.Div_Un">Div_Un</see> instruction depending on whether the type is signed or not.
        /// </param>
        public void Div(Type type)
        {
            Emit(Unsigned(type) ? OpCodes.Div_Un : OpCodes.Div);
        }

        /// <summary>
        /// Divides two values and pushes the remainder onto the evaluation stack.
        /// </summary>
        /// <param name="type">
        /// The <see cref="Type">Type</see> of the values being divided.
        /// <para></para>
        /// Emits either <see cref="OpCodes.Rem">Rem</see> or <see cref="OpCodes.Rem_Un">Rem_Un</see> instruction depending on whether the type is signed or not.
        /// </param>
        public void Rem(Type type)
        {
            Emit(Unsigned(type) ? OpCodes.Rem_Un : OpCodes.Rem);
        }

        /// <summary>
        /// Shifts an integer value to the left (in zeroes) by a specified number of bits, pushing the result onto the evaluation stack.
        /// </summary>
        public void Shl()
        {
            Emit(OpCodes.Shl);
        }

        /// <summary>
        /// Shifts an integer value to the right by a specified number of bits, pushing the result onto the evaluation stack.
        /// </summary>
        /// <param name="type">
        /// The <see cref="Type">Type</see> of the value being shifted.
        /// <para></para>
        /// Emits either <see cref="OpCodes.Shr">Shr</see> or <see cref="OpCodes.Shr_Un">Shr_Un</see> instruction depending on whether the type is signed or not.
        /// </param>
        public void Shr(Type type)
        {
            Emit(Unsigned(type) ? OpCodes.Shr_Un : OpCodes.Shr);
        }

        /// <summary>
        /// Negates a value and pushes the result onto the evaluation stack.
        /// </summary>
        public void Neg()
        {
            Emit(OpCodes.Neg);
        }

        /// <summary>
        /// Computes the bitwise complement of the integer value on top of the stack and pushes the result onto the evaluation stack as the same type.
        /// </summary>
        public void Not()
        {
            Emit(OpCodes.Not);
        }

        /// <summary>
        /// Pushes a new object reference to a string literal stored in the metadata.
        /// </summary>
        /// <param name="value">The value to push.</param>
        public void Ldstr(string value)
        {
            Emit(OpCodes.Ldstr, value);
        }

        /// <summary>
        /// Converts the value on top of the evaluation stack to int8, then extends (pads) it to int32.
        /// </summary>
        public void Conv_I1()
        {
            Emit(OpCodes.Conv_I1);
        }

        /// <summary>
        /// Converts the value on top of the evaluation stack to unsigned int8, and extends it to int32.
        /// </summary>
        public void Conv_U1()
        {
            Emit(OpCodes.Conv_U1);
        }

        /// <summary>
        /// Converts the value on top of the evaluation stack to int16, then extends (pads) it to int32.
        /// </summary>
        public void Conv_I2()
        {
            Emit(OpCodes.Conv_I2);
        }

        /// <summary>
        /// Converts the value on top of the evaluation stack to unsigned int16, and extends it to int32.
        /// </summary>
        public void Conv_U2()
        {
            Emit(OpCodes.Conv_U2);
        }

        /// <summary>
        /// Converts the value on top of the evaluation stack to int32.
        /// </summary>
        public void Conv_I4()
        {
            Emit(OpCodes.Conv_I4);
        }

        /// <summary>
        /// Converts the value on top of the evaluation stack to unsigned int32, and extends it to int32.
        /// </summary>
        public void Conv_U4()
        {
            Emit(OpCodes.Conv_U4);
        }

        /// <summary>
        /// Converts the value on top of the evaluation stack to int64.
        /// </summary>
        public void Conv_I8()
        {
            Emit(OpCodes.Conv_I8);
        }

        /// <summary>
        /// Converts the value on top of the evaluation stack to unsigned int64, and extends it to int64.
        /// </summary>
        public void Conv_U8()
        {
            Emit(OpCodes.Conv_U8);
        }

        /// <summary>
        /// Converts the value on top of the evaluation stack to unsigned native int, and extends it to native int.
        /// </summary>
        public void Conv_U()
        {
            Emit(OpCodes.Conv_U);
        }

        /// <summary>
        /// Converts the value on top of the evaluation stack to float32.
        /// </summary>
        public void Conv_R4()
        {
            Emit(OpCodes.Conv_R4);
        }

        /// <summary>
        /// Converts the value on top of the evaluation stack to float64.
        /// </summary>
        public void Conv_R8()
        {
            Emit(OpCodes.Conv_R8);
        }

        /// <summary>
        /// Converts the unsigned integer value on top of the evaluation stack to float32.
        /// </summary>
        public void Conv_R_Un()
        {
            Emit(OpCodes.Conv_R_Un);
        }

        /// <summary>
        /// Converts the value on top of the evaluation stack to signed int8 and extends it to int32, throwing <see cref="OverflowException">OverflowException</see> on overflow.
        /// </summary>
        /// <param name="type">
        /// The <see cref="Type">Type</see> of the value being converted.
        /// <para></para>
        /// Emits either <see cref="OpCodes.Conv_Ovf_I1">Conv_Ovf_I1</see> or <see cref="OpCodes.Conv_Ovf_I1_Un">Conv_Ovf_I1_Un</see> instruction depending on whether the type is signed or not.
        /// </param>
        public void Conv_Ovf_I1(Type type)
        {
            if(type == null)
                throw new ArgumentNullException("type");
            Emit(Unsigned(type) ? OpCodes.Conv_Ovf_I1_Un : OpCodes.Conv_Ovf_I1);
        }

        /// <summary>
        /// Converts the value on top of the evaluation stack to signed int16 and extending it to int32, throwing <see cref="OverflowException">OverflowException</see> on overflow.
        /// </summary>
        /// <param name="type">
        /// The <see cref="Type">Type</see> of the value being converted.
        /// <para></para>
        /// Emits either <see cref="OpCodes.Conv_Ovf_I2">Conv_Ovf_I2</see> or <see cref="OpCodes.Conv_Ovf_I2_Un">Conv_Ovf_I2_Un</see> instruction depending on whether the type is signed or not.
        /// </param>
        public void Conv_Ovf_I2(Type type)
        {
            if(type == null)
                throw new ArgumentNullException("type");
            Emit(Unsigned(type) ? OpCodes.Conv_Ovf_I2_Un : OpCodes.Conv_Ovf_I2);
        }

        /// <summary>
        /// Converts the value on top of the evaluation stack to signed int32, throwing <see cref="OverflowException">OverflowException</see> on overflow.
        /// </summary>
        /// <param name="type">
        /// The <see cref="Type">Type</see> of the value being converted.
        /// <para></para>
        /// Emits either <see cref="OpCodes.Conv_Ovf_I4">Conv_Ovf_I4</see> or <see cref="OpCodes.Conv_Ovf_I4_Un">Conv_Ovf_I4_Un</see> instruction depending on whether the type is signed or not.
        /// </param>
        public void Conv_Ovf_I4(Type type)
        {
            if(type == null)
                throw new ArgumentNullException("type");
            Emit(Unsigned(type) ? OpCodes.Conv_Ovf_I4_Un : OpCodes.Conv_Ovf_I4);
        }

        /// <summary>
        /// Converts the value on top of the evaluation stack to signed int64, throwing <see cref="OverflowException">OverflowException</see> on overflow.
        /// </summary>
        /// <param name="type">
        /// The <see cref="Type">Type</see> of the value being converted.
        /// <para></para>
        /// Emits either <see cref="OpCodes.Conv_Ovf_I8">Conv_Ovf_I8</see> or <see cref="OpCodes.Conv_Ovf_I8_Un">Conv_Ovf_I8_Un</see> instruction depending on whether the type is signed or not.
        /// </param>
        public void Conv_Ovf_I8(Type type)
        {
            if(type == null)
                throw new ArgumentNullException("type");
            Emit(Unsigned(type) ? OpCodes.Conv_Ovf_I8_Un : OpCodes.Conv_Ovf_I8);
        }

        /// <summary>
        /// Converts the value on top of the evaluation stack to unsigned int8 and extends it to int32, throwing <see cref="OverflowException">OverflowException</see> on overflow.
        /// </summary>
        /// <param name="type">
        /// The <see cref="Type">Type</see> of the value being converted.
        /// <para></para>
        /// Emits either <see cref="OpCodes.Conv_Ovf_U1">Conv_Ovf_U1</see> or <see cref="OpCodes.Conv_Ovf_U1_Un">Conv_Ovf_U1_Un</see> instruction depending on whether the type is signed or not.
        /// </param>
        public void Conv_Ovf_U1(Type type)
        {
            if(type == null)
                throw new ArgumentNullException("type");
            Emit(Unsigned(type) ? OpCodes.Conv_Ovf_U1_Un : OpCodes.Conv_Ovf_U1);
        }

        /// <summary>
        /// Converts the value on top of the evaluation stack to unsigned int16 and extends it to int32, throwing <see cref="OverflowException">OverflowException</see> on overflow.
        /// </summary>
        /// <param name="type">
        /// The <see cref="Type">Type</see> of the value being converted.
        /// <para></para>
        /// Emits either <see cref="OpCodes.Conv_Ovf_U2">Conv_Ovf_U2</see> or <see cref="OpCodes.Conv_Ovf_U2_Un">Conv_Ovf_U2_Un</see> instruction depending on whether the type is signed or not.
        /// </param>
        public void Conv_Ovf_U2(Type type)
        {
            if(type == null)
                throw new ArgumentNullException("type");
            Emit(Unsigned(type) ? OpCodes.Conv_Ovf_U2_Un : OpCodes.Conv_Ovf_U2);
        }

        /// <summary>
        /// Converts the value on top of the evaluation stack to unsigned int32, throwing <see cref="OverflowException">OverflowException</see> on overflow.
        /// </summary>
        /// <param name="type">
        /// The <see cref="Type">Type</see> of the value being converted.
        /// <para></para>
        /// Emits either <see cref="OpCodes.Conv_Ovf_U4">Conv_Ovf_U4</see> or <see cref="OpCodes.Conv_Ovf_U4_Un">Conv_Ovf_U4_Un</see> instruction depending on whether the type is signed or not.
        /// </param>
        public void Conv_Ovf_U4(Type type)
        {
            if(type == null)
                throw new ArgumentNullException("type");
            Emit(Unsigned(type) ? OpCodes.Conv_Ovf_U4_Un : OpCodes.Conv_Ovf_U4);
        }

        /// <summary>
        /// Converts the value on top of the evaluation stack to unsigned int64, throwing <see cref="OverflowException">OverflowException</see> on overflow.
        /// </summary>
        /// <param name="type">
        /// The <see cref="Type">Type</see> of the value being converted.
        /// <para></para>
        /// Emits either <see cref="OpCodes.Conv_Ovf_U8">Conv_Ovf_U8</see> or <see cref="OpCodes.Conv_Ovf_U8_Un">Conv_Ovf_U8_Un</see> instruction depending on whether the type is signed or not.
        /// </param>
        public void Conv_Ovf_U8(Type type)
        {
            if(type == null)
                throw new ArgumentNullException("type");
            Emit(Unsigned(type) ? OpCodes.Conv_Ovf_U8_Un : OpCodes.Conv_Ovf_U8);
        }

        /// <summary>
        /// Calls the method indicated by the passed method descriptor.
        /// <para></para>
        /// Emits a <see cref="OpCodes.Call">Call</see> or <see cref="OpCodes.Callvirt">Callvirt</see> instruction depending on whether the method is a virtual or not.
        /// </summary>
        /// <param name="method">The <see cref="MethodInfo">Method</see> to be called.</param>
        /// <param name="type">The <see cref="Type">Type</see> of an object to call the method on</param>
        /// <param name="optionalParameterTypes">The types of the optional arguments if the method is a varargs method; otherwise, null.</param>
        public void Call(MethodInfo method, Type type = null, Type[] optionalParameterTypes = null)
        {
            OpCode opCode = method.IsVirtual ? OpCodes.Callvirt : OpCodes.Call;
            if(opCode == OpCodes.Callvirt)
            {
                if(type == null)
                    throw new ArgumentNullException("type", "Type must be specified for a virtual method call");
                if(type.IsValueType)
                    Emit(OpCodes.Constrained, type);
            }
            var parameter = new MethodILInstructionParameter(method);
            var lineNumber = ilCode.Append(opCode, parameter, new EmptyILInstructionComment());
            if(analyzeStack && stack != null)
                MutateStack(opCode, parameter);
            ilCode.SetComment(lineNumber, GetComment());
            il.EmitCall(opCode, method, optionalParameterTypes);
        }

        /// <summary>
        /// Calls a late-bound method on an object, pushing the return value onto the evaluation stack.
        /// </summary>
        /// <param name="method">The <see cref="MethodInfo">Method</see> to be called.</param>
        /// <param name="type">The <see cref="Type">Type</see> of an object to call the method on</param>
        /// <param name="optionalParameterTypes">The types of the optional arguments if the method is a varargs method; otherwise, null.</param>
        public void Callvirt(MethodInfo method, Type type, Type[] optionalParameterTypes = null)
        {
            OpCode opCode = OpCodes.Callvirt;
            if(type == null)
                throw new ArgumentNullException("type", "Type must be specified for a virtual method call");
            if(type.IsValueType)
                Emit(OpCodes.Constrained, type);
            var parameter = new MethodILInstructionParameter(method);
            var lineNumber = ilCode.Append(opCode, parameter, new EmptyILInstructionComment());
            if(analyzeStack && stack != null)
                MutateStack(opCode, parameter);
            ilCode.SetComment(lineNumber, GetComment());
            il.EmitCall(opCode, method, optionalParameterTypes);
        }

        /// <summary>
        /// Statically calls the method indicated by the passed method descriptor.
        /// </summary>
        /// <param name="method">The <see cref="MethodInfo">Method</see> to be called.</param>
        /// <param name="optionalParameterTypes">The types of the optional arguments if the method is a varargs method; otherwise, null.</param>
        public void Callnonvirt(MethodInfo method, Type[] optionalParameterTypes = null)
        {
            OpCode opCode = OpCodes.Call;
            var parameter = new MethodILInstructionParameter(method);
            var lineNumber = ilCode.Append(opCode, parameter, new EmptyILInstructionComment());
            if(analyzeStack && stack != null)
                MutateStack(opCode, parameter);
            ilCode.SetComment(lineNumber, GetComment());
            il.EmitCall(opCode, method, optionalParameterTypes);
        }

        /// <summary>
        /// Calls the method indicated on the evaluation stack (as a pointer to an entry point) with arguments described by a calling convention.
        /// </summary>
        /// <param name="callingConvention">The managed calling convention to be used.</param>
        /// <param name="returnType">The <see cref="Type">Type</see> of the result.</param>
        /// <param name="parameterTypes">The types of the required arguments to the instruction.</param>
        /// <param name="optionalParameterTypes">The types of the optional arguments for varargs calls.</param>
        public void Calli(CallingConventions callingConvention, Type returnType, Type[] parameterTypes, Type[] optionalParameterTypes = null)
        {
            var parameter = new MethodByAddressILInstructionParameter(returnType, parameterTypes);
            var lineNumber = ilCode.Append(OpCodes.Calli, parameter, new EmptyILInstructionComment());
            if(analyzeStack && stack != null)
                MutateStack(OpCodes.Calli, parameter);
            ilCode.SetComment(lineNumber, GetComment());
            il.EmitCalli(OpCodes.Calli, callingConvention, returnType, parameterTypes, optionalParameterTypes);
        }

        public class Label
        {
            public Label(System.Reflection.Emit.Label label, string name)
            {
                this.label = label;
                this.name = name;
            }

            public static implicit operator System.Reflection.Emit.Label(Label label)
            {
                return label.label;
            }

            public string Name { get { return name; } }

            private readonly System.Reflection.Emit.Label label;
            private readonly string name;
        }

        public class Local
        {
            public Local(LocalBuilder localBuilder, string name)
            {
                this.localBuilder = localBuilder;
                this.name = name;
            }

            public static implicit operator LocalBuilder(Local local)
            {
                return local.localBuilder;
            }

            public string Name { get { return name; } }
            public Type Type { get { return localBuilder.LocalType; } }

            private readonly LocalBuilder localBuilder;
            private readonly string name;
        }

        internal readonly Dictionary<Label, Type[]> labelStacks = new Dictionary<Label, Type[]>();
        internal readonly ILCode ilCode = new ILCode();
        internal readonly Type methodReturnType;
        internal readonly Type[] methodParameterTypes;

        private void MutateStack(OpCode opCode, ILInstructionParameter parameter)
        {
            StackMutatorCollection.Mutate(opCode, this, parameter, ref stack);
        }

        private static bool IsStruct(Type type)
        {
            return type.IsValueType && !type.IsPrimitive && !type.IsEnum && type != typeof(IntPtr) && type != typeof(UIntPtr);
        }

        private static bool Unsigned(Type type)
        {
            if(type == typeof(IntPtr))
                return false;
            if(type == typeof(UIntPtr))
                return true;
            switch(Type.GetTypeCode(type))
            {
            case TypeCode.Boolean:
            case TypeCode.Byte:
            case TypeCode.Char:
            case TypeCode.UInt16:
            case TypeCode.UInt32:
            case TypeCode.UInt64:
                return true;
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32:
            case TypeCode.Int64:
            case TypeCode.Single:
            case TypeCode.Double:
                return false;
            default:
                throw new NotSupportedException("Type '" + type.Name + "' is not supported");
            }
        }

        private ILInstructionComment GetComment()
        {
            return stack == null ? (ILInstructionComment)new InaccessibleCodeILInstructionComment() : new StackILInstructionComment(stack.Reverse().ToArray());
        }

        private void Emit(OpCode opCode, ILInstructionParameter parameter)
        {
            var lineNumber = ilCode.Append(opCode, new EmptyILInstructionComment());
            if(analyzeStack && stack != null)
                MutateStack(opCode, parameter);
            ilCode.SetComment(lineNumber, GetComment());
            il.Emit(opCode);
        }

        private void Emit(OpCode opCode)
        {
            var lineNumber = ilCode.Append(opCode, new EmptyILInstructionComment());
            if(analyzeStack && stack != null)
                MutateStack(opCode, null);
            ilCode.SetComment(lineNumber, GetComment());
            il.Emit(opCode);
        }

        private void Emit(OpCode opCode, Local local)
        {
            var parameter = new LocalILInstructionParameter(local);
            var lineNumber = ilCode.Append(opCode, parameter, new EmptyILInstructionComment());
            if(analyzeStack && stack != null)
                MutateStack(opCode, parameter);
            ilCode.SetComment(lineNumber, GetComment());
            il.Emit(opCode, local);
        }

        private void Emit(OpCode opCode, Type type)
        {
            var parameter = new TypeILInstructionParameter(type);
            var lineNumber = ilCode.Append(opCode, parameter, new EmptyILInstructionComment());
            if(analyzeStack && stack != null)
                MutateStack(opCode, parameter);
            ilCode.SetComment(lineNumber, GetComment());
            il.Emit(opCode, type);
        }

        private void Emit(OpCode opCode, byte value)
        {
            var parameter = new PrimitiveILInstructionParameter(value);
            var lineNumber = ilCode.Append(opCode, parameter, new EmptyILInstructionComment());
            if(analyzeStack && stack != null)
                MutateStack(opCode, parameter);
            ilCode.SetComment(lineNumber, GetComment());
            il.Emit(opCode, value);
        }

        private void Emit(OpCode opCode, int value)
        {
            var parameter = new PrimitiveILInstructionParameter(value);
            var lineNumber = ilCode.Append(opCode, parameter, new EmptyILInstructionComment());
            if(analyzeStack && stack != null)
                MutateStack(opCode, parameter);
            ilCode.SetComment(lineNumber, GetComment());
            il.Emit(opCode, value);
        }

        private void Emit(OpCode opCode, sbyte value)
        {
            var parameter = new PrimitiveILInstructionParameter(value);
            var lineNumber = ilCode.Append(opCode, parameter, new EmptyILInstructionComment());
            if(analyzeStack && stack != null)
                MutateStack(opCode, parameter);
            ilCode.SetComment(lineNumber, GetComment());
            il.Emit(opCode, value);
        }

        private void Emit(OpCode opCode, long value)
        {
            var parameter = new PrimitiveILInstructionParameter(value);
            var lineNumber = ilCode.Append(opCode, parameter, new EmptyILInstructionComment());
            if(analyzeStack && stack != null)
                MutateStack(opCode, parameter);
            ilCode.SetComment(lineNumber, GetComment());
            il.Emit(opCode, value);
        }

        private void Emit(OpCode opCode, double value)
        {
            var parameter = new PrimitiveILInstructionParameter(value);
            var lineNumber = ilCode.Append(opCode, parameter, new EmptyILInstructionComment());
            if(analyzeStack && stack != null)
                MutateStack(opCode, parameter);
            ilCode.SetComment(lineNumber, GetComment());
            il.Emit(opCode, value);
        }

        private void Emit(OpCode opCode, float value)
        {
            var parameter = new PrimitiveILInstructionParameter(value);
            var lineNumber = ilCode.Append(opCode, parameter, new EmptyILInstructionComment());
            if(analyzeStack && stack != null)
                MutateStack(opCode, parameter);
            ilCode.SetComment(lineNumber, GetComment());
            il.Emit(opCode, value);
        }

        private void Emit(OpCode opCode, string value)
        {
            var parameter = new StringILInstructionParameter(value);
            var lineNumber = ilCode.Append(opCode, parameter, new EmptyILInstructionComment());
            if(analyzeStack && stack != null)
                MutateStack(opCode, parameter);
            ilCode.SetComment(lineNumber, GetComment());
            il.Emit(opCode, value);
        }

        private void Emit(OpCode opCode, Label label)
        {
            var parameter = new LabelILInstructionParameter(label);
            var lineNumber = ilCode.Append(opCode, parameter, new EmptyILInstructionComment());
            if(analyzeStack && stack != null)
                MutateStack(opCode, parameter);
            ilCode.SetComment(lineNumber, GetComment());
            il.Emit(opCode, label);
        }

        private void Emit(OpCode opCode, Label[] labels)
        {
            var parameter = new LabelsILInstructionParameter(labels);
            var lineNumber = ilCode.Append(opCode, parameter, new EmptyILInstructionComment());
            if(analyzeStack && stack != null)
                MutateStack(opCode, parameter);
            ilCode.SetComment(lineNumber, GetComment());
            il.Emit(opCode, labels.Select(label => (System.Reflection.Emit.Label)label).ToArray());
        }

        private void Emit(OpCode opCode, FieldInfo field)
        {
            var parameter = new FieldILInstructionParameter(field);
            var lineNumber = ilCode.Append(opCode, parameter, new EmptyILInstructionComment());
            if(analyzeStack && stack != null)
                MutateStack(opCode, parameter);
            ilCode.SetComment(lineNumber, GetComment());
            il.Emit(opCode, field);
        }

        private void Emit(OpCode opCode, MethodInfo method)
        {
            var parameter = new MethodILInstructionParameter(method);
            var lineNumber = ilCode.Append(opCode, parameter, new EmptyILInstructionComment());
            if(analyzeStack && stack != null)
                MutateStack(opCode, parameter);
            ilCode.SetComment(lineNumber, GetComment());
            il.Emit(opCode, method);
        }

        private void Emit(OpCode opCode, ConstructorInfo constructor)
        {
            var parameter = new ConstructorILInstructionParameter(constructor);
            var lineNumber = ilCode.Append(opCode, parameter, new EmptyILInstructionComment());
            if(analyzeStack && stack != null)
                MutateStack(opCode, parameter);
            ilCode.SetComment(lineNumber, GetComment());
            il.Emit(opCode, constructor);
        }

        private int localId;
        private int labelId;

        private Stack<Type> stack = new Stack<Type>();

        private readonly ILGenerator il;
        private readonly bool analyzeStack = true;
    }

    // ReSharper restore InconsistentNaming
}