namespace Statik.Server.Rendering;

public class UserPageRenderException(string message, Exception? innerException = null)
    : Exception(message, innerException);
