template<typename T>
inline T findMSBU32(T x)
{
    T v = select(x, T(-1) - x, x < T(0));
    return select(clz(T(0)) - (clz(v) + T(1)), T(-1), v == T(0));
}
