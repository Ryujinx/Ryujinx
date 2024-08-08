template<typename T>
inline T findLSB(T x)
{
    return select(ctz(x), T(-1), x == T(0));
}
