﻿using ATMPlus.Helpers;
using Microsoft.EntityFrameworkCore;
using osu.Framework.Lists;
using System;
using System.Linq;

namespace ATMPlus.Database
{
    public class DatabaseStore : DbContext
    {
#pragma warning disable IDE1006 // Estilos de nombres
        DbSet<Cuenta> Cuenta { get; set; }
        DbSet<CuentaGerente> CuentaGerente { get; set; }
        DbSet<CuentaCliente> CuentaCliente { get; set; }
        DbSet<HistorialConsulta> HistorialConsulta { get; set; }
        DbSet<HistorialRetiro> HistorialRetiro { get; set; }
        DbSet<HistorialDeposito> HistorialDeposito { get; set; }

#pragma warning restore IDE1006 // Estilos de nombres

        public static int BilletesRestantes { get; private set; } = 500;
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(
              @"Data Source=ColdVolcano\MSSQLSERVER01;" +
              "Initial Catalog=ATM_Data;" +
              "Integrated Security=true;" +
              "MultipleActiveResultSets=true;");
        }

        public SortedList<int> ObtenerCuentas(CuentaGerente cuenta)
        {
            var retorno = new SortedList<int>();
            foreach (var acc in CuentaCliente)
                retorno.Add(acc.NumeroCuenta);
            return retorno;
        }

        private SortedList<IHistorial> historial(int cuenta = 0)
        {
            var historial = new SortedList<IHistorial>();
            historial.AddRange(HistorialDeposito.Where(c => cuenta == 0 ? true : cuenta == c.CuentaOrigen || cuenta == c.CuentaDestino));
            historial.AddRange(HistorialRetiro.Where(c => cuenta == 0 ? true : cuenta == c.CuentaOrigen));
            return historial;
        }

        public SortedList<IHistorial> ObtenerHistorial(CuentaGerente auth) => historial();

        public SortedList<IHistorial> ObtenerHistorial(CuentaCliente cuenta)
        {
            if (cuenta == null)
                return null;

            HistorialConsulta.Add(new HistorialConsulta() { FechaHora = DateTime.Now, CuentaOrigen = cuenta.NumeroCuenta });
            SaveChanges();

            return historial(cuenta.NumeroCuenta);
        }

        public ResultadoOperacion RetiroUsuario(CuentaCliente cuenta, double cantidad)
        {
            if (cuenta.Saldo < cantidad)
                return ResultadoOperacion.NoSaldo;
            if (cantidad > BilletesRestantes * 20)
                return ResultadoOperacion.NoDinero; 
            
            CuentaCliente updateAcc = CuentaCliente.First(acc => cuenta.NumeroCuenta == acc.NumeroCuenta);
            updateAcc.Saldo -= cantidad;

            cuenta.Saldo = updateAcc.Saldo;

            HistorialRetiro.Add(new HistorialRetiro() { FechaHora = DateTime.Now, CuentaOrigen = cuenta.NumeroCuenta, Cantidad = cantidad });
            SaveChanges();

            BilletesRestantes -= (int)(cantidad / 20);

            return ResultadoOperacion.Correcto;
        }

        public ResultadoOperacion DepositoUsuario(CuentaCliente cuenta, double cantidad, int cuentaDestino, out PendingDeposit salida)
        {
            salida = new PendingDeposit();
            if (CuentaCliente.FirstOrDefault(acc => cuentaDestino == acc.NumeroCuenta) == null)
                return ResultadoOperacion.NoCuenta;

            salida = new PendingDeposit(DateTime.Now, cuentaDestino, cantidad);
            return ResultadoOperacion.Correcto;
        }

        public void CerrarSesion(CuentaCliente cuenta)
        {
            foreach (var ent in cuenta.DepositosPendientes)
            {
                CuentaCliente updateAcc = CuentaCliente.First(acc => ent.Destination == acc.NumeroCuenta);
                updateAcc.Saldo += ent.Ammount;
                HistorialDeposito.Add(new HistorialDeposito() { Cantidad = ent.Ammount, CuentaOrigen = cuenta.NumeroCuenta, CuentaDestino = ent.Destination, FechaHora = ent.Time });
            }

            SaveChanges();
        }

        public ICuenta InicioSesion(int numCuenta, string pinPass)
        {
            var cuenta = Cuenta.FirstOrDefault(c => c.NumeroCuenta == numCuenta && c.PinPass == pinPass);
            if (cuenta == null)
                return null;
            if (cuenta.TipoCuenta == TipoCuenta.Gerente)
            {
                var cuentaGerente = CuentaGerente.First(c => cuenta.NumeroCuenta == c.NumeroCuenta);
                cuentaGerente.Nombre = new Nombre(cuenta);
                return cuentaGerente;
            }
            else if (cuenta.TipoCuenta == TipoCuenta.Cliente)
            {
                var cuentaGerente = CuentaCliente.First(c => cuenta.NumeroCuenta == c.NumeroCuenta);
                cuentaGerente.Nombre = new Nombre(cuenta);
                return cuentaGerente;
            }
            return null;
        }
    }
}
