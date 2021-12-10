/*
 * By default, the JIT emits code in the prologue of every function to initialize the stack space allocated for local variables to zero.
 * Due to C#'s definite assignment rule, which prevents access to a local before it is assigned, this is completely pointless (with three exceptions*). 
 * For this reason, the dotnet runtime applies this attribute to supress the zero-initialization behavior, which results in a small but measurable performance boost.
 * see: https://github.com/dotnet/aspnetcore/issues/26586#issuecomment-703346754
 *
 * The three exceptions to the definite assignment rule are:
 * 1. stackalloc
 *      With SkipLocalsInit enabled, Spans that have been created with stackalloc will contain garbage data that can be accessed.
 *      Care must be taken to fully write to every element of the Span in such circumstances. stackalloc should only be used by experienced C# devs!
 *
 * 2. pointer to a local
 *      When using unsafe code, it is possible to take the address of a local variable, and by this indirection, access its value before it has been assigned to.
 *      If you're smart enough to be messing with pointers to locals in C#, you're smart enough to assign values to your variables.
 *
 * 3. out params of P/Invoked methods.
 *      Since such methods are implemented in native code, the C# rule that out params must be assigned before the function returns cannot be enforced.
 *      So a native method could return without initializing a local passed as an out param, but the compiler will assume it was initialized.
 *
 *      This is the most dangerous of the 3 exceptions! Always manually zero-initialize locals passed as out params to native methods.
 */
[module: System.Runtime.CompilerServices.SkipLocalsInit]