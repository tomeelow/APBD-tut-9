namespace Tutorial9.Exceptions;

public class NotFoundException   : Exception { public NotFoundException(string msg) : base(msg) { } }
public class BadRequestException : Exception { public BadRequestException(string msg) : base(msg) { } }
public class ConflictException   : Exception { public ConflictException(string msg) : base(msg) { } }